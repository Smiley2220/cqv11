using CampusQ.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace CampusQ.API.Hubs;

public sealed class QueueHub : Hub
{
    public static string OfficeGroup(string office) => $"office:{office.Trim().ToLowerInvariant()}";
    public static string TicketGroup(int ticketNumber) => $"ticket:{ticketNumber}";
    public Task SubscribeToOffice(string office) => Groups.AddToGroupAsync(Context.ConnectionId, OfficeGroup(office));
    public Task SubscribeToTicket(int ticketNumber) => Groups.AddToGroupAsync(Context.ConnectionId, TicketGroup(ticketNumber));
}

public interface IQueueEventPublisher { Task PublishAsync(QueueEvent e, CancellationToken ct = default); }
public sealed class SignalRQueueEventPublisher(IHubContext<QueueHub> hub) : IQueueEventPublisher
{
    public Task PublishAsync(QueueEvent e, CancellationToken ct = default)
    {
        var tasks = new List<Task> { hub.Clients.Group(QueueHub.OfficeGroup(e.Service)).SendAsync("queueChanged", e, ct) };
        if (e.TicketNumber is int n) tasks.Add(hub.Clients.Group(QueueHub.TicketGroup(n)).SendAsync("ticketChanged", e, ct));
        if (!string.IsNullOrWhiteSpace(e.TargetService)) tasks.Add(hub.Clients.Group(QueueHub.OfficeGroup(e.TargetService)).SendAsync("queueChanged", e, ct));
        return Task.WhenAll(tasks);
    }
}
