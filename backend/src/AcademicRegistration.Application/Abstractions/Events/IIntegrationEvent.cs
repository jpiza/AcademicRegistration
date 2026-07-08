namespace AcademicRegistration.Application.Abstractions.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    string EventType { get; }

    DateTime OccurredOnUtc { get; }

    string PartitionKey { get; }
}
