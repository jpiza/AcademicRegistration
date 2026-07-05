using AcademicRegistration.Application.Abstractions.Messaging;
using AcademicRegistration.Application.DTOs;

namespace AcademicRegistration.Application.Subjects.Queries.GetSubjects;

public sealed record GetSubjectsQuery : IQuery<IReadOnlyList<SubjectDto>>;
