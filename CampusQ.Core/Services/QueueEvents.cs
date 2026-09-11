namespace CampusQ.Core.Services;

public enum QueueEventType
{
    TicketAdded,
    TicketServed,
    TicketTransferred,
    QueueChanged
}

public sealed record QueueEvent(
    QueueEventType Type,
    string Service,
    int? TicketNumber,
    DateTimeOffset OccurredAt,
    string? TargetService = null);

public sealed record TransferTicketRequest(
    int TicketNumber,
    string TargetService,
    string Reason);

public sealed record TransferTicketResult(
    int TicketNumber,
    string SourceService,
    string TargetService,
    DateTimeOffset TransferredAt);
