using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AcademicRegistration.Infrastructure.Persistence.Repositories;

internal sealed class ProfessorRepository : IProfessorRepository
{
    private readonly AcademicRegistrationDbContext _dbContext;

    public ProfessorRepository(AcademicRegistrationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Professor>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Professors
            .AsNoTracking()
            .OrderBy(professor => professor.FullName)
            .ToListAsync(cancellationToken);
    }
}
