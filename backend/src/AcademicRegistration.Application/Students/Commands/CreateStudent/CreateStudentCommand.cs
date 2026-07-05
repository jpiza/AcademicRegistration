using AcademicRegistration.Application.Abstractions.Messaging;

namespace AcademicRegistration.Application.Students.Commands.CreateStudent;

public sealed record CreateStudentCommand(
    string Name,
    string Email,
    string DocumentNumber,
    IReadOnlyCollection<Guid> SubjectIds) : ICommand<ErrorOr<Guid>>;
