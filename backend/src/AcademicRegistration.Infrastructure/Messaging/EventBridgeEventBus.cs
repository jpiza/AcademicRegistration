using AcademicRegistration.Application.Abstractions.Events;
using AcademicRegistration.Infrastructure.Outbox;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcademicRegistration.Infrastructure.Messaging;

public sealed class EventBridgeEventBus : IOutboxMessagePublisher
{
    private readonly IAmazonEventBridge _eventBridge;
    private readonly EventBridgeSettings _settings;
    private readonly ILogger<EventBridgeEventBus> _logger;

    public EventBridgeEventBus(
        IAmazonEventBridge eventBridge,
        IOptions<EventBridgeSettings> settings,
        ILogger<EventBridgeEventBus> logger)
    {
        _eventBridge = eventBridge;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<EventPublishResult> PublishAsync(
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        var request = new PutEventsRequest
        {
            Entries =
            [
                new PutEventsRequestEntry
                {
                    EventBusName = _settings.EventBusName,
                    Source = _settings.Source,
                    DetailType = outboxMessage.EventType,
                    Detail = outboxMessage.Payload,
                    Time = outboxMessage.OccurredOnUtc,
                    Resources =
                    [
                        $"academic-registration:partition-key:{outboxMessage.PartitionKey}"
                    ]
                }
            ]
        };

        var response = await _eventBridge.PutEventsAsync(request, cancellationToken);
        var entry = response.Entries.SingleOrDefault();

        if (entry is null)
        {
            throw new InvalidOperationException("EventBridge did not return a result for the outbox message.");
        }

        if (!string.IsNullOrWhiteSpace(entry.ErrorCode))
        {
            throw new InvalidOperationException(
                $"EventBridge rejected outbox message {outboxMessage.Id}. {entry.ErrorCode}: {entry.ErrorMessage}");
        }

        _logger.LogInformation(
            "Evento publicado en EventBridge. EventBus: {EventBusName}, EventBridgeEventId: {EventBridgeEventId}, EventType: {EventType}, OutboxMessageId: {OutboxMessageId}",
            _settings.EventBusName,
            entry.EventId,
            outboxMessage.EventType,
            outboxMessage.Id);

        return new EventPublishResult($"eventbridge {_settings.EventBusName} | eventId: {entry.EventId}");
    }
}
