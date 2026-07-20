using AcademicRegistration.Application.Abstractions.Events;

namespace AcademicRegistration.Infrastructure.Outbox;

public interface IOutboxMessagePublisher
{
    Task<EventPublishResult> PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
