using AcademicRegistration.Application.Abstractions.Events;

namespace AcademicRegistration.Application.IntegrationEvents.Students;

public sealed record StudentNotificationRequestedIntegrationEvent(
    Guid EventId,
    string EventType,
    Guid StudentId,
    string Name,
    string Email,
    IReadOnlyCollection<StudentNotificationSubject> Subjects,
    DateTime OccurredOnUtc) : IIntegrationEvent
{
    public string PartitionKey => StudentId.ToString();
}
