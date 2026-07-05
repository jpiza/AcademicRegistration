using AcademicRegistration.Application.Abstractions.Messaging;

namespace AcademicRegistration.Application.Students.Commands.UpdateStudent;

public sealed record UpdateStudentCommand(
    Guid StudentId,
    string Name,
    string Email,
    string DocumentNumber,
    IReadOnlyCollection<Guid> SubjectIds) : ICommand<ErrorOr<Unit>>;
