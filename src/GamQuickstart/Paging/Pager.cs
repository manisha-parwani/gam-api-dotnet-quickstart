using System.Runtime.CompilerServices;

namespace GamQuickstart.Paging;

/// <summary>
/// Reads every result of a paged query using a limit/offset loop.
/// </summary>
/// <remarks>
/// Ad Manager "get by statement" calls return at most one page per call. To read everything you
/// ask for page 1 (offset 0), then move the offset forward by the number of results you got,
/// until you have read <see cref="Page{T}.TotalResultSetSize"/> results.
/// </remarks>
public static class Pager
{
    /// <summary>The largest page size this app asks for. It matches the library's suggested page limit.</summary>
    public const int MaxPageSize = 500;

    /// <summary>
    /// Streams results page by page.
    /// </summary>
    /// <param name="fetchPage">Fetches one page. Arguments are (offset, limit, cancellationToken).</param>
    /// <param name="pageSize">How many results to ask for per call (1 to <see cref="MaxPageSize"/>).</param>
    /// <param name="maxItems">Stop after this many results. Null means read everything.</param>
    /// <param name="cancellationToken">Stops the loop between pages.</param>
    public static IAsyncEnumerable<T> ReadAllAsync<T>(
        Func<int, int, CancellationToken, Task<Page<T>>> fetchPage,
        int pageSize,
        int? maxItems = null,
        CancellationToken cancellationToken = default)
    {
        // Validate here, not inside the iterator, so bad arguments fail at the call site.
        ArgumentNullException.ThrowIfNull(fetchPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        if (maxItems is int max)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(max, 1, nameof(maxItems));
        }

        return ReadAllCoreAsync(fetchPage, pageSize, maxItems, cancellationToken);
    }

    private static async IAsyncEnumerable<T> ReadAllCoreAsync<T>(
        Func<int, int, CancellationToken, Task<Page<T>>> fetchPage,
        int pageSize,
        int? maxItems,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var offset = 0;
        var returned = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Do not ask for more than we still need when --top is set.
            var limit = maxItems is int max ? Math.Min(pageSize, max - returned) : pageSize;
            var page = await fetchPage(offset, limit, cancellationToken);

            foreach (var item in page.Items)
            {
                yield return item;
                returned++;
                if (returned == maxItems)
                {
                    yield break;
                }
            }

            offset += page.Items.Count;

            // An empty page also ends the loop, so a wrong total can never cause an endless loop.
            if (page.Items.Count == 0 || offset >= page.TotalResultSetSize)
            {
                yield break;
            }
        }
    }
}
