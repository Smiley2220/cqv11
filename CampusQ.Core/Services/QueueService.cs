using System;
using System.Collections.Generic;
using System.Linq;
using CampusQ.MVP.Data;
using CampusQ.MVP.Models;
using Microsoft.Extensions.Logging;

namespace CampusQ.Core.Services
{
    /// <summary>
    /// Business logic service for queue operations.
    /// Orchestrates data access and applies business rules.
    /// Separates concerns from UI presentation (MVP pattern).
    /// </summary>
    public class QueueService
    {
        private readonly QueueRepository _queueRepository;
        private readonly ILogger? _logger;

        public QueueService(QueueRepository queueRepository, ILogger? logger = null)
        {
            _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
            _logger = logger;
        }

        /// <summary>
        /// Gets all queue entries (active tickets).
        /// </summary>
        public List<QueueEntry> GetAllQueue()
        {
            try
            {
                _logger?.LogDebug("Retrieving all queue entries");
                return _queueRepository.GetAll() ?? new List<QueueEntry>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve all queue entries");
                throw;
            }
        }

        /// <summary>
        /// Gets queue entries for a specific service.
        /// </summary>
        public List<QueueEntry> GetQueueByService(string service)
        {
            try
            {
                _logger?.LogDebug("Retrieving queue entries for service: {Service}", service);
                return _queueRepository.GetAllByService(service) ?? new List<QueueEntry>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve queue for service: {Service}", service);
                throw;
            }
        }

        /// <summary>
        /// Gets a specific ticket from the active queue.
        /// </summary>
        public QueueEntry? GetTicket(int ticketNumber)
        {
            try
            {
                _logger?.LogDebug("Retrieving ticket: {TicketNumber}", ticketNumber);
                return _queueRepository.GetByTicketNumber(ticketNumber);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve ticket: {TicketNumber}", ticketNumber);
                throw;
            }
        }

        /// <summary>
        /// Adds a new entry to the queue.
        /// Returns the newly created ticket.
        /// </summary>
        public QueueEntry AddToQueue(string purpose, string service)
        {
            try
            {
                _logger?.LogInformation("Adding ticket to queue - Service: {Service}, Purpose: {Purpose}", service, purpose);

                var entry = new QueueEntry
                {
                    Purpose = purpose,
                    Service = service,
                    TimeAdded = DateTime.Now
                };

                _queueRepository.Add(entry);

                _logger?.LogInformation("Ticket added successfully - TicketNumber: {TicketNumber}, ServiceNumber: {ServiceNumber}",
                    entry.TicketNumber, entry.ServiceTicketNumber);

                return entry;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add ticket to queue");
                throw;
            }
        }

        /// <summary>
        /// Gets position of a ticket in queue ahead of it.
        /// </summary>
        public int GetPositionInQueue(int ticketNumber, string service, string purpose)
        {
            try
            {
                _logger?.LogDebug("Calculating queue position for ticket: {TicketNumber}", ticketNumber);
                return _queueRepository.CountAhead(service, purpose, ticketNumber);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to calculate position for ticket: {TicketNumber}", ticketNumber);
                throw;
            }
        }

        /// <summary>
        /// Gets queue statistics for a service.
        /// </summary>
        public QueueStatistics GetQueueStats(string service)
        {
            try
            {
                var queueEntries = GetQueueByService(service);
                return new QueueStatistics
                {
                    Service = service,
                    TotalInQueue = queueEntries.Count,
                    FirstTicketTime = queueEntries.FirstOrDefault()?.TimeAdded,
                    LastTicketTime = queueEntries.LastOrDefault()?.TimeAdded,
                    AverageWaitMinutes = CalculateAverageWait(queueEntries)
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to calculate stats for service: {Service}", service);
                throw;
            }
        }

        /// <summary>
        /// Clears all entries from the active queue.
        /// </summary>
        public void ClearQueue()
        {
            try
            {
                _logger?.LogWarning("Clearing entire queue - this is an admin action");
                _queueRepository.ClearAll();
                _logger?.LogInformation("Queue cleared successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to clear queue");
                throw;
            }
        }

        /// <summary>
        /// Gets archived queue entries (history).
        /// </summary>
        public List<QueuePersistDto> GetQueueHistory(int? serviceTicketNumber = null)
        {
            try
            {
                _logger?.LogDebug("Retrieving queue history");
                var history = _queueRepository.GetHistoryAll() ?? new List<QueuePersistDto>();

                if (serviceTicketNumber.HasValue)
                {
                    history = history.Where(h => h.ServiceTicketNumber == serviceTicketNumber).ToList();
                }

                return history;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve queue history");
                throw;
            }
        }

        /// <summary>
        /// Gets a specific historical entry.
        /// </summary>
        public QueuePersistDto? GetHistoryEntry(int ticketNumber)
        {
            try
            {
                _logger?.LogDebug("Retrieving history for ticket: {TicketNumber}", ticketNumber);
                return _queueRepository.GetHistoryByTicketNumber(ticketNumber);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve history for ticket: {TicketNumber}", ticketNumber);
                throw;
            }
        }

        /// <summary>
        /// Calculates average wait time in minutes.
        /// </summary>
        private int CalculateAverageWait(List<QueueEntry> entries)
        {
            if (entries.Count == 0)
                return 0;

            var now = DateTime.Now;
            var totalMinutes = entries.Sum(e => (now - e.TimeAdded).TotalMinutes);
            return (int)(totalMinutes / entries.Count);
        }
    }

    /// <summary>
    /// Queue statistics DTO.
    /// </summary>
    public class QueueStatistics
    {
        public string? Service { get; set; }
        public int TotalInQueue { get; set; }
        public DateTime? FirstTicketTime { get; set; }
        public DateTime? LastTicketTime { get; set; }
        public int AverageWaitMinutes { get; set; }
    }
}
