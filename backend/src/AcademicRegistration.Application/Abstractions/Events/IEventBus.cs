namespace AcademicRegistration.Application.Abstractions.Events;

public interface IEventBus
{
    Task<EventPublishResult> PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}

public record EventPublishResult(string Id);
