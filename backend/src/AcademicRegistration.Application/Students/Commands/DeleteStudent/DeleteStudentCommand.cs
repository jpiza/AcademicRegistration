using AcademicRegistration.Application.Abstractions.Messaging;

namespace AcademicRegistration.Application.Students.Commands.DeleteStudent;

public sealed record DeleteStudentCommand(Guid StudentId) : ICommand<ErrorOr<Unit>>;
