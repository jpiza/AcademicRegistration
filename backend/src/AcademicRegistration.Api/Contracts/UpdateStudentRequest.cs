namespace AcademicRegistration.Api.Contracts;

public sealed record UpdateStudentRequest(
    string Name,
    string Email,
    string DocumentNumber,
    IReadOnlyCollection<Guid> SubjectIds);
