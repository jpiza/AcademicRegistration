namespace AcademicRegistration.Application.DTOs;

public sealed record SubjectDto(
    Guid Id,
    string Code,
    string Name,
    int Credits,
    Guid ProfessorId,
    string ProfessorName);
