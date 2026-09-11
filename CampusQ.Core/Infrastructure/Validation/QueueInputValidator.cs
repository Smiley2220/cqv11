using System;
using System.Collections.Generic;

namespace CampusQ.Core.Infrastructure.Validation
{
    /// <summary>
    /// Provides input validation and sanitization for queue operations.
    /// All user inputs are validated before database operations.
    /// </summary>
    public class QueueInputValidator
    {
        private const int MaxPurposeLength = 200;
        private const int MaxServiceNameLength = 100;

        /// <summary>
        /// Validates a queue entry for integrity and security.
        /// Throws ArgumentException if validation fails.
        /// </summary>
        public static void ValidateQueueEntry(string? purpose, string? service)
        {
            if (string.IsNullOrWhiteSpace(purpose))
            {
                throw new ArgumentException("Purpose cannot be empty or null.", nameof(purpose));
            }

            if (string.IsNullOrWhiteSpace(service))
            {
                throw new ArgumentException("Service cannot be empty or null.", nameof(service));
            }

            if (purpose.Length > MaxPurposeLength)
            {
                throw new ArgumentException($"Purpose cannot exceed {MaxPurposeLength} characters.", nameof(purpose));
            }

            if (service.Length > MaxServiceNameLength)
            {
                throw new ArgumentException($"Service name cannot exceed {MaxServiceNameLength} characters.", nameof(service));
            }

            // Validate service name against allowed services
            var allowedServices = new[] { "Admission", "Cashier", "Registrar" };
            if (Array.IndexOf(allowedServices, service) < 0)
            {
                throw new ArgumentException($"Service must be one of: {string.Join(", ", allowedServices)}", nameof(service));
            }
        }

        /// <summary>
        /// Validates a ticket number is positive and reasonable.
        /// </summary>
        public static void ValidateTicketNumber(int ticketNumber)
        {
            if (ticketNumber <= 0)
            {
                throw new ArgumentException("Ticket number must be greater than zero.", nameof(ticketNumber));
            }

            if (ticketNumber > int.MaxValue)
            {
                throw new ArgumentException("Ticket number is invalid.", nameof(ticketNumber));
            }
        }

        /// <summary>
        /// Validates a service name against allowed values.
        /// </summary>
        public static void ValidateServiceName(string? service)
        {
            if (string.IsNullOrWhiteSpace(service))
            {
                throw new ArgumentException("Service name cannot be empty or null.", nameof(service));
            }

            if (service.Length > MaxServiceNameLength)
            {
                throw new ArgumentException($"Service name cannot exceed {MaxServiceNameLength} characters.", nameof(service));
            }

            var allowedServices = new[] { "Admission", "Cashier", "Registrar" };
            if (Array.IndexOf(allowedServices, service) < 0)
            {
                throw new ArgumentException($"Service must be one of: {string.Join(", ", allowedServices)}", nameof(service));
            }
        }
    }

    /// <summary>
    /// Custom exception for validation errors.
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Custom exception for repository/data access errors.
    /// </summary>
    public class RepositoryException : Exception
    {
        public RepositoryException(string message) : base(message) { }
        public RepositoryException(string message, Exception innerException) : base(message, innerException) { }
    }
}
