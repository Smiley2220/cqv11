using Microsoft.Extensions.Logging;
using System;

namespace CampusQ.Core.Infrastructure.Logging
{
    /// <summary>
    /// Provides structured logging methods that replace Debug.WriteLine calls.
    /// Supports different log levels (Information, Warning, Error, Debug).
    /// </summary>
    public class ApplicationLogger
    {
        private readonly ILogger _logger;

        public ApplicationLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LogInfo(string message, params object[] args)
        {
            _logger.LogInformation(message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            _logger.LogWarning(message, args);
        }

        public void LogError(string message, Exception? ex = null, params object[] args)
        {
            if (ex != null)
            {
                _logger.LogError(ex, message, args);
            }
            else
            {
                _logger.LogError(message, args);
            }
        }

        public void LogDebug(string message, params object[] args)
        {
            _logger.LogDebug(message, args);
        }

        public void LogTrace(string message, params object[] args)
        {
            _logger.LogTrace(message, args);
        }
    }
}
