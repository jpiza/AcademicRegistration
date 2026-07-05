using AcademicRegistration.Application.DTOs;
using AcademicRegistration.Application.Interfaces;
using AcademicRegistration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AcademicRegistration.Infrastructure.Persistence.Repositories;

internal sealed class StudentReadRepository : IStudentReadRepository
{
    private readonly AcademicRegistrationDbContext _dbContext;

    public StudentReadRepository(AcademicRegistrationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<StudentSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var students = await StudentQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return students
            .OrderBy(student => student.Name.Value)
            .Select(MapSummary)
            .ToList();
    }

    public async Task<StudentDetailsDto?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var student = await StudentQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == studentId, cancellationToken);

        return student is null ? null : MapDetails(student);
    }

    public async Task<IReadOnlyList<ClassmatesBySubjectDto>?> GetClassmatesAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await StudentQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == studentId, cancellationToken);

        if (student is null)
        {
            return null;
        }

        var subjectIds = student.Enrollments.Select(enrollment => enrollment.SubjectId).ToArray();

        var classmatesQuery = StudentQuery()
            .AsNoTracking()
            .Where(candidate => candidate.Id != studentId);

        classmatesQuery = WhereSharesAnySubject(classmatesQuery, subjectIds);

        var classmates = await classmatesQuery.ToListAsync(cancellationToken);

        return student.Enrollments
            .OrderBy(enrollment => enrollment.Subject?.Name)
            .Select(enrollment => new ClassmatesBySubjectDto(
                enrollment.SubjectId,
                enrollment.Subject?.Name ?? string.Empty,
                enrollment.Subject?.Professor?.FullName ?? string.Empty,
                classmates
                    .Where(classmate => classmate.Enrollments.Any(candidateEnrollment =>
                        candidateEnrollment.SubjectId == enrollment.SubjectId))
                    .Select(classmate => classmate.Name.Value)
                    .Order()
                    .ToList()))
            .ToList();
    }

    private IQueryable<Student> StudentQuery()
    {
        return _dbContext.Students
            .Include(student => student.Enrollments)
            .ThenInclude(enrollment => enrollment.Subject)
            .ThenInclude(subject => subject!.Professor);
    }

    private static IQueryable<Student> WhereSharesAnySubject(IQueryable<Student> query, IReadOnlyList<Guid> subjectIds)
    {
        return subjectIds.Count switch
        {
            0 => query.Where(_ => false),
            1 => query.Where(student =>
                student.Enrollments.Any(enrollment => enrollment.SubjectId == subjectIds[0])),
            2 => query.Where(student =>
                student.Enrollments.Any(enrollment =>
                    enrollment.SubjectId == subjectIds[0]
                    || enrollment.SubjectId == subjectIds[1])),
            _ => query.Where(student =>
                student.Enrollments.Any(enrollment =>
                    enrollment.SubjectId == subjectIds[0]
                    || enrollment.SubjectId == subjectIds[1]
                    || enrollment.SubjectId == subjectIds[2]))
        };
    }

    private static StudentSummaryDto MapSummary(Student student)
    {
        var subjects = MapSubjects(student);

        return new StudentSummaryDto(
            student.Id,
            student.Name.Value,
            student.Email.Value,
            student.DocumentNumber.Value,
            subjects.Sum(subject => subject.Credits),
            subjects);
    }

    private static StudentDetailsDto MapDetails(Student student)
    {
        var subjects = MapSubjects(student);

        return new StudentDetailsDto(
            student.Id,
            student.Name.Value,
            student.Email.Value,
            student.DocumentNumber.Value,
            student.CreatedAtUtc,
            student.UpdatedAtUtc,
            subjects.Sum(subject => subject.Credits),
            subjects);
    }

    private static IReadOnlyList<StudentSubjectDto> MapSubjects(Student student)
    {
        return student.Enrollments
            .OrderBy(enrollment => enrollment.Subject?.Name)
            .Select(enrollment => new StudentSubjectDto(
                enrollment.SubjectId,
                enrollment.Subject?.Code.Value ?? string.Empty,
                enrollment.Subject?.Name ?? string.Empty,
                enrollment.Subject?.Credits.Value ?? 0,
                enrollment.Subject?.ProfessorId ?? Guid.Empty,
                enrollment.Subject?.Professor?.FullName ?? string.Empty))
            .ToList();
    }
}
