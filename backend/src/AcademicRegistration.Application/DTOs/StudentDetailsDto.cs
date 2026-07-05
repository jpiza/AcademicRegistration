namespace AcademicRegistration.Application.DTOs;

public sealed record StudentDetailsDto(
    Guid Id,
    string Name,
    string Email,
    string DocumentNumber,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int TotalCredits,
    IReadOnlyList<StudentSubjectDto> Subjects);
