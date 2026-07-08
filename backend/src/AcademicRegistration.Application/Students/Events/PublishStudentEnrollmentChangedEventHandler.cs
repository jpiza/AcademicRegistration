using AcademicRegistration.Application.Abstractions.Events;
using AcademicRegistration.Application.IntegrationEvents.Students;
using AcademicRegistration.Domain.Events;

namespace AcademicRegistration.Application.Students.Events;

internal sealed class PublishStudentEnrollmentChangedEventHandler
    : INotificationHandler<StudentEnrollmentChangedEvent>
{
    private readonly IEventBus _eventBus;

    public PublishStudentEnrollmentChangedEventHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(
        StudentEnrollmentChangedEvent notification,
        CancellationToken cancellationToken)
    {
        var integrationEvent = new StudentNotificationRequestedIntegrationEvent(
            Guid.NewGuid(),
            StudentNotificationEventTypes.StudentEnrollmentChanged,
            notification.StudentId,
            notification.Name,
            notification.Email,
            notification.Subjects
                .Select(subject => new StudentNotificationSubject(
                    subject.SubjectId,
                    subject.Code,
                    subject.Name))
                .ToArray(),
            notification.OccurredOnUtc);

        await _eventBus.PublishAsync(integrationEvent, cancellationToken);
    }
}
