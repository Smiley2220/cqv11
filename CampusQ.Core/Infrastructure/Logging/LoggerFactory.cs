using Microsoft.Extensions.Logging;

namespace CampusQ.Core.Infrastructure.Logging
{
    /// <summary>
    /// Provides logging helper methods for libraries and non-DI applications.
    /// This is primarily used for backward compatibility with code that doesn't use dependency injection.
    /// </summary>
    public static class LoggerHelper
    {
        /// <summary>
        /// Creates a null logger (no-op) for the specified category name.
        /// Override this or inject ILoggerFactory in DI-enabled applications.
        /// </summary>
        public static ILogger CreateLogger(string categoryName)
        {
            return new NullLogger();
        }
    }

    /// <summary>
    /// A null logger implementation that discards all log messages.
    /// Used as a default for non-DI applications.
    /// </summary>
    public class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
