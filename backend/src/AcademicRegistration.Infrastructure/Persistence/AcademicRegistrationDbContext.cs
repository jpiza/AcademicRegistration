using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.Primitives;
using AcademicRegistration.Infrastructure.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AcademicRegistration.Infrastructure.Persistence;

public sealed class AcademicRegistrationDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher _publisher;

    public AcademicRegistrationDbContext(DbContextOptions<AcademicRegistrationDbContext> options, IPublisher publisher)
        : base(options)
    {
        _publisher = publisher;
    }

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<Professor> Professors => Set<Professor>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicRegistrationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.GetDomainEvents().Count != 0)
            .ToList();

        var domainEvents = aggregates
            .SelectMany(entity => entity.GetDomainEvents())
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        aggregates.ForEach(entity => entity.ClearDomainEvents());

        return result;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkTransaction(transaction);
    }
}
