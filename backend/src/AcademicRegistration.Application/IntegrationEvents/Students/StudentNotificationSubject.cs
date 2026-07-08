namespace AcademicRegistration.Application.IntegrationEvents.Students;

public sealed record StudentNotificationSubject(Guid SubjectId, string Code, string Name);
