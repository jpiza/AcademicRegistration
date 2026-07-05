using AcademicRegistration.Application.DTOs;
using AcademicRegistration.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AcademicRegistration.Infrastructure.Persistence.Repositories;

internal sealed class SubjectReadRepository : ISubjectReadRepository
{
    private readonly AcademicRegistrationDbContext _dbContext;

    public SubjectReadRepository(AcademicRegistrationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SubjectDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var subjects = await _dbContext.Subjects
            .Include(subject => subject.Professor)
            .AsNoTracking()
            .OrderBy(subject => subject.Name)
            .ToListAsync(cancellationToken);

        return subjects
            .Select(subject => new SubjectDto(
                subject.Id,
                subject.Code.Value,
                subject.Name,
                subject.Credits.Value,
                subject.ProfessorId,
                subject.Professor?.FullName ?? string.Empty))
            .ToList();
    }
}
