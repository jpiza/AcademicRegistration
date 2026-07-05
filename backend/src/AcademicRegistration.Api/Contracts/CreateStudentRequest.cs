namespace AcademicRegistration.Api.Contracts;

public sealed record CreateStudentRequest(
    string Name,
    string Email,
    string DocumentNumber,
    IReadOnlyCollection<Guid> SubjectIds);
