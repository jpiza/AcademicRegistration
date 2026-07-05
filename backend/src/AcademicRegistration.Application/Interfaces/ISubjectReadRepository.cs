using AcademicRegistration.Application.DTOs;

namespace AcademicRegistration.Application.Interfaces;

public interface ISubjectReadRepository
{
    Task<IReadOnlyList<SubjectDto>> ListAsync(CancellationToken cancellationToken = default);
}
