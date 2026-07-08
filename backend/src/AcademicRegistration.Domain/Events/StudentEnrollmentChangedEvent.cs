using AcademicRegistration.Domain.Primitives;

namespace AcademicRegistration.Domain.Events;

public sealed record StudentEnrollmentChangedEvent(
    Guid StudentId,
    string Name,
    string Email,
    IReadOnlyCollection<StudentEnrollmentSubject> Subjects)
    : DomainEvent(StudentId);
