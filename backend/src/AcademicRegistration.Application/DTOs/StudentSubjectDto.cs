namespace AcademicRegistration.Application.DTOs;

public sealed record StudentSubjectDto(
    Guid SubjectId,
    string Code,
    string Name,
    int Credits,
    Guid ProfessorId,
    string ProfessorName);
