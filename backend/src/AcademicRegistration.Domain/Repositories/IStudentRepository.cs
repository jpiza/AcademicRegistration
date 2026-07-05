using AcademicRegistration.Domain.Entities;

namespace AcademicRegistration.Domain.Repositories;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, Guid? excludingStudentId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByDocumentNumberAsync(string documentNumber, Guid? excludingStudentId = null, CancellationToken cancellationToken = default);

    void Add(Student student);

    void Remove(Student student);
}
