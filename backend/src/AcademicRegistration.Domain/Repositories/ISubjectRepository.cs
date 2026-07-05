using AcademicRegistration.Domain.Entities;

namespace AcademicRegistration.Domain.Repositories;

public interface ISubjectRepository
{
    Task<IReadOnlyList<Subject>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subject>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
