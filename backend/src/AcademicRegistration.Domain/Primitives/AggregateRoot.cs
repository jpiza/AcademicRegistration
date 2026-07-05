namespace AcademicRegistration.Domain.Primitives;

public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = [];

    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
