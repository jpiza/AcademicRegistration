namespace AcademicRegistration.Application.DTOs;

public sealed record StudentSummaryDto(
    Guid Id,
    string Name,
    string Email,
    string DocumentNumber,
    int TotalCredits,
    IReadOnlyList<StudentSubjectDto> Subjects);
