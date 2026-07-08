using AcademicRegistration.Application.Abstractions.Events;
using AcademicRegistration.Application.IntegrationEvents.Students;
using AcademicRegistration.Domain.Events;

namespace AcademicRegistration.Application.Students.Events;

internal sealed class PublishStudentRegisteredEventHandler
    : INotificationHandler<StudentRegisteredEvent>
{
    private readonly IEventBus _eventBus;

    public PublishStudentRegisteredEventHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(
        StudentRegisteredEvent notification,
        CancellationToken cancellationToken)
    {
        var integrationEvent = new StudentNotificationRequestedIntegrationEvent(
            Guid.NewGuid(),
            StudentNotificationEventTypes.StudentRegistered,
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
