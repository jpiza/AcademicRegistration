using AcademicRegistration.Domain.Entities;

namespace AcademicRegistration.Domain.Repositories;

public interface IProfessorRepository
{
    Task<IReadOnlyList<Professor>> ListAsync(CancellationToken cancellationToken = default);
}
