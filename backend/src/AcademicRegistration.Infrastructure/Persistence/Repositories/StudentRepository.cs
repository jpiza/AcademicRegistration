using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.Repositories;
using AcademicRegistration.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AcademicRegistration.Infrastructure.Persistence.Repositories;

internal sealed class StudentRepository : IStudentRepository
{
    private readonly AcademicRegistrationDbContext _dbContext;

    public StudentRepository(AcademicRegistrationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Students
            .Include(student => student.Enrollments)
            .FirstOrDefaultAsync(student => student.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        Guid? excludingStudentId = null,
        CancellationToken cancellationToken = default)
    {
        var emailAddress = EmailAddress.Create(email);

        return _dbContext.Students.AnyAsync(
            student => student.Email == emailAddress && (!excludingStudentId.HasValue || student.Id != excludingStudentId.Value),
            cancellationToken);
    }

    public Task<bool> ExistsByDocumentNumberAsync(
        string documentNumber,
        Guid? excludingStudentId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedDocumentNumber = DocumentNumber.Create(documentNumber);

        return _dbContext.Students.AnyAsync(
            student => student.DocumentNumber == normalizedDocumentNumber && (!excludingStudentId.HasValue || student.Id != excludingStudentId.Value),
            cancellationToken);
    }

    public void Add(Student student)
    {
        _dbContext.Students.Add(student);
    }

    public void Remove(Student student)
    {
        _dbContext.Students.Remove(student);
    }
}
