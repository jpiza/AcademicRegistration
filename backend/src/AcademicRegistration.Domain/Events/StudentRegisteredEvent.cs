using AcademicRegistration.Domain.Primitives;

namespace AcademicRegistration.Domain.Events;

public sealed record StudentRegisteredEvent(Guid StudentId, string Name, string Email)
    : DomainEvent(StudentId);
