using Microsoft.Extensions.Configuration;
using System;

namespace CampusQ.Core.Infrastructure.Configuration
{
    /// <summary>
    /// Centralized configuration service for database connections and other settings.
    /// Loads configuration from environment variables, appsettings.json, and defaults.
    /// </summary>
    public class ConfigurationService
    {
        private readonly IConfiguration? _configuration;

        public ConfigurationService(IConfiguration? configuration = null)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Gets the database connection string.
        /// Priority: Environment variable > appsettings > default
        /// </summary>
        public string GetDatabaseConnectionString()
        {
            // Try environment variable first (highest priority for production)
            var envConnStr = Environment.GetEnvironmentVariable("CAMPUSQ_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(envConnStr))
            {
                return envConnStr;
            }

            // Try configuration (from appsettings.json)
            var configConnStr = _configuration?.GetConnectionString("CampusQ");
            if (!string.IsNullOrEmpty(configConnStr))
            {
                return configConnStr;
            }

            // Fallback default for development
            return "Data Source=.\\SQLEXPRESS;Initial Catalog=CampusQ;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        }

        /// <summary>
        /// Gets a generic configuration value.
        /// </summary>
        public string? GetSetting(string key, string? defaultValue = null)
        {
            // Try environment variable first
            var envValue = Environment.GetEnvironmentVariable($"CAMPUSQ_{key.ToUpper()}");
            if (!string.IsNullOrEmpty(envValue))
            {
                return envValue;
            }

            // Try configuration
            var configValue = _configuration?[key];
            if (!string.IsNullOrEmpty(configValue))
            {
                return configValue;
            }

            return defaultValue;
        }
    }
}
