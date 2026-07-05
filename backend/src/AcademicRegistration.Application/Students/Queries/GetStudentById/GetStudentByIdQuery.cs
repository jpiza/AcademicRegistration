using AcademicRegistration.Application.Abstractions.Messaging;
using AcademicRegistration.Application.DTOs;

namespace AcademicRegistration.Application.Students.Queries.GetStudentById;

public sealed record GetStudentByIdQuery(Guid StudentId) : IQuery<ErrorOr<StudentDetailsDto>>;
