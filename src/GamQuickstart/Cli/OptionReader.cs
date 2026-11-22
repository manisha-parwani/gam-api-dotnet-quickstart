using System.Globalization;

namespace GamQuickstart.Cli;

/// <summary>
/// Splits <c>--name value</c>, <c>--name=value</c> and <c>--flag</c> tokens for one command,
/// then reads and validates typed values. Every problem is collected, not just the first one.
/// </summary>
internal sealed class OptionReader
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public OptionReader(
        string command,
        IEnumerable<string> tokens,
        IReadOnlySet<string> valueOptions,
        IReadOnlySet<string> flagOptions)
    {
        var pending = new Queue<string>(tokens);

        while (pending.TryDequeue(out var token))
        {
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                Errors.Add($"Unexpected argument '{token}'. Options start with '--'.");
                continue;
            }

            var equals = token.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? token : token[..equals];
            var inlineValue = equals < 0 ? null : token[(equals + 1)..];

            if (_values.ContainsKey(name) || _flags.Contains(name))
            {
                Errors.Add($"{name} was given more than once.");
                continue;
            }

            if (flagOptions.Contains(name))
            {
                if (inlineValue is not null)
                {
                    Errors.Add($"{name} is a flag and does not take a value.");
                }

                _flags.Add(name);
            }
            else if (valueOptions.Contains(name))
            {
                var value = inlineValue
                    ?? (pending.TryPeek(out var next) && !next.StartsWith("--", StringComparison.Ordinal) ? pending.Dequeue() : null);
                if (string.IsNullOrWhiteSpace(value))
                {
                    Errors.Add($"{name} needs a value.");
                    continue;
                }

                _values[name] = value;
            }
            else
            {
                Errors.Add($"Unknown option {name} for '{command}'.");

                // Skip the unknown option's value too, so it is not reported a second time.
                if (inlineValue is null && pending.TryPeek(out var next) && !next.StartsWith("--", StringComparison.Ordinal))
                {
                    pending.Dequeue();
                }
            }
        }
    }

    public List<string> Errors { get; } = [];

    public bool HasFlag(string name) => _flags.Contains(name);

    public string? GetString(string name, int maxLength, bool required = false)
    {
        if (!TryGetRaw(name, required, out var raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.Length > maxLength)
        {
            Errors.Add($"{name} must be at most {maxLength} characters.");
            return null;
        }

        return value;
    }

    public int? GetInt(string name, int min, int max, bool required = false)
    {
        if (!TryGetRaw(name, required, out var raw))
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
        {
            return value;
        }

        Errors.Add(max == int.MaxValue
            ? $"{name} must be a whole number of {min} or more (got '{raw}')."
            : $"{name} must be a whole number from {min} to {max} (got '{raw}').");
        return null;
    }

    public long? GetPositiveLong(string name, bool required = false)
    {
        if (!TryGetRaw(name, required, out var raw))
        {
            return null;
        }

        if (long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return value;
        }

        Errors.Add($"{name} must be a positive whole number (got '{raw}').");
        return null;
    }

    public Uri? GetHttpUrl(string name, bool required = false)
    {
        if (!TryGetRaw(name, required, out var raw))
        {
            return null;
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return uri;
        }

        Errors.Add($"{name} must be an absolute http:// or https:// URL (got '{raw}').");
        return null;
    }

    private bool TryGetRaw(string name, bool required, out string raw)
    {
        if (_values.TryGetValue(name, out var value))
        {
            raw = value;
            return true;
        }

        if (required)
        {
            Errors.Add($"{name} is required.");
        }

        raw = string.Empty;
        return false;
    }
}
