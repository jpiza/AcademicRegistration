using AcademicRegistration.Application.Abstractions.Messaging;
using AcademicRegistration.Application.DTOs;

namespace AcademicRegistration.Application.Students.Queries.GetStudentClassmates;

public sealed record GetStudentClassmatesQuery(Guid StudentId) : IQuery<ErrorOr<IReadOnlyList<ClassmatesBySubjectDto>>>;
