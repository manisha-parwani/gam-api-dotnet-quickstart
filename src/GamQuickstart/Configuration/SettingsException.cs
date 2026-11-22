namespace GamQuickstart.Configuration;

/// <summary>The settings are missing or invalid. The message says what to fix.</summary>
public sealed class SettingsException : Exception
{
    public SettingsException()
        : base("The Ad Manager settings are missing or invalid.")
    {
    }

    public SettingsException(string message)
        : base(message)
    {
    }

    public SettingsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
