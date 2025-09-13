using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Centralized logging service that handles debug panel output with verbosity filtering
/// </summary>
public class LoggingService
{
    private readonly MainForm _mainForm;
    private AppSettings? _settings;

    public LoggingService(MainForm mainForm)
    {
        _mainForm = mainForm;
    }

    /// <summary>
    /// Updates the settings reference for verbosity filtering
    /// </summary>
    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Logs a message with the specified level to the debug panel
    /// </summary>
    /// <param name="level">The log level of the message</param>
    /// <param name="message">The message to log</param>
    public void Log(LogLevel level, string message)
    {
        var verbosity = _settings?.ConsoleVerbosity ?? VerbosityLevel.Normal;
        
        // Determine if this message should be logged based on verbosity level
        if (!ShouldLogMessage(level, verbosity))
            return;

        // Format the message with level prefix
        var formattedMessage = FormatMessage(level, message);
        
        // Send to debug panel
        _mainForm.LogConsole(formattedMessage);
    }

    /// <summary>
    /// Determines if a message should be logged based on log level and verbosity setting
    /// </summary>
    private static bool ShouldLogMessage(LogLevel level, VerbosityLevel verbosity)
    {
        return verbosity switch
        {
            VerbosityLevel.Minimal => level == LogLevel.Error,
            VerbosityLevel.Normal => level <= LogLevel.Info,     // Error, Warning, and Info (no Debug)
            VerbosityLevel.Verbose => level <= LogLevel.Info,    // Error, Warning, and Info (no Debug)
            VerbosityLevel.Debug => true,  // Show all messages including Debug
            _ => true
        };
    }

    /// <summary>
    /// Formats a message with appropriate level prefix
    /// </summary>
    private static string FormatMessage(LogLevel level, string message)
    {
        return level switch
        {
            LogLevel.Error => $"ERROR: {message}",
            LogLevel.Warning => $"WARNING: {message}",
            LogLevel.Info => message,
            LogLevel.Debug => $"DEBUG: {message}",
            _ => message
        };
    }

    // Convenience methods for common log levels
    public void LogError(string message) => Log(LogLevel.Error, message);
    public void LogWarning(string message) => Log(LogLevel.Warning, message);
    public void LogInfo(string message) => Log(LogLevel.Info, message);
    public void LogVerbose(string message) => Log(LogLevel.Info, message); // Verbose uses Info level
    public void LogDebug(string message) => Log(LogLevel.Debug, message);
}
