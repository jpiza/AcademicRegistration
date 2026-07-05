using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.Repositories;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace AcademicRegistration.Infrastructure.Persistence.Repositories;

internal sealed class SubjectRepository : ISubjectRepository
{
    private readonly AcademicRegistrationDbContext _dbContext;

    public SubjectRepository(AcademicRegistrationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Subject>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subjects
            .Include(subject => subject.Professor)
            .AsNoTracking()
            .OrderBy(subject => subject.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subject>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = ids.Distinct().ToArray();

        if (distinctIds.Length == 0)
        {
            return [];
        }

        return await _dbContext.Subjects
            .Where(HasAnyId(distinctIds))
            .ToListAsync(cancellationToken);
    }

    private static Expression<Func<Subject, bool>> HasAnyId(IEnumerable<Guid> ids)
    {
        var subject = Expression.Parameter(typeof(Subject), "subject");
        var subjectId = Expression.Property(subject, nameof(Subject.Id));

        Expression? predicate = null;

        foreach (var id in ids)
        {
            var matchesId = Expression.Equal(subjectId, Expression.Constant(id));
            predicate = predicate is null ? matchesId : Expression.OrElse(predicate, matchesId);
        }

        return Expression.Lambda<Func<Subject, bool>>(predicate ?? Expression.Constant(false), subject);
    }
}
