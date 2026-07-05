using MediatR;

namespace AcademicRegistration.Domain.Primitives;

public abstract record DomainEvent(Guid Id) : INotification
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
