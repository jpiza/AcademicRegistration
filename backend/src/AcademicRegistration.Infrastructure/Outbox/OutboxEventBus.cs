using System.Text.Json;
using AcademicRegistration.Application.Abstractions.Events;
using AcademicRegistration.Infrastructure.Persistence;

namespace AcademicRegistration.Infrastructure.Outbox;

public sealed class OutboxEventBus : IEventBus
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AcademicRegistrationDbContext _dbContext;

    public OutboxEventBus(AcademicRegistrationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<EventPublishResult> PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var payload = JsonSerializer.Serialize(integrationEvent, JsonOptions);
        var message = OutboxMessage.Create(
            integrationEvent.EventId,
            typeof(TEvent).FullName ?? typeof(TEvent).Name,
            integrationEvent.EventType,
            integrationEvent.PartitionKey,
            payload,
            integrationEvent.OccurredOnUtc);

        _dbContext.OutboxMessages.Add(message);

        return Task.FromResult<EventPublishResult>(new OutboxEventPublishResult(message.Id));
    }
}
