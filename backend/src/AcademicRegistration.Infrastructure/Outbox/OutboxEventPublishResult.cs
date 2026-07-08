using AcademicRegistration.Application.Abstractions.Events;

namespace AcademicRegistration.Infrastructure.Outbox;

public sealed record OutboxEventPublishResult(Guid MessageId)
    : EventPublishResult($"outbox message {MessageId}");
