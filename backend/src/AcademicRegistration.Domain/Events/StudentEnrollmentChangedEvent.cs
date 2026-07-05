using AcademicRegistration.Domain.Primitives;

namespace AcademicRegistration.Domain.Events;

public sealed record StudentEnrollmentChangedEvent(Guid StudentId, IReadOnlyCollection<Guid> SubjectIds)
    : DomainEvent(StudentId);
