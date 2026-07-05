using AcademicRegistration.Application.Abstractions.Messaging;
using AcademicRegistration.Application.DTOs;

namespace AcademicRegistration.Application.Students.Queries.GetStudents;

public sealed record GetStudentsQuery : IQuery<IReadOnlyList<StudentSummaryDto>>;
