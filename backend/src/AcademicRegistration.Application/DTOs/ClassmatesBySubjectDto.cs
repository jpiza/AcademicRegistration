namespace AcademicRegistration.Application.DTOs;

public sealed record ClassmatesBySubjectDto(
    Guid SubjectId,
    string SubjectName,
    string ProfessorName,
    IReadOnlyList<string> ClassmateNames);
