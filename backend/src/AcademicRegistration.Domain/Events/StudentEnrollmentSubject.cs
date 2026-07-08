namespace AcademicRegistration.Domain.Events;

public sealed record StudentEnrollmentSubject(Guid SubjectId, string Code, string Name);
