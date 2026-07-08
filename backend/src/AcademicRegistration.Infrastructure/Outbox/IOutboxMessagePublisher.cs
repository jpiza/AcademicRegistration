using AcademicRegistration.Infrastructure.Messaging;

namespace AcademicRegistration.Infrastructure.Outbox;

public interface IOutboxMessagePublisher
{
    Task<KafkaEventMetadata> PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
