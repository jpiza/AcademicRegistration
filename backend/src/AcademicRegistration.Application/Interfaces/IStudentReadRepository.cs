using AcademicRegistration.Application.DTOs;

namespace AcademicRegistration.Application.Interfaces;

public interface IStudentReadRepository
{
    Task<IReadOnlyList<StudentSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<StudentDetailsDto?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassmatesBySubjectDto>?> GetClassmatesAsync(Guid studentId, CancellationToken cancellationToken = default);
}
