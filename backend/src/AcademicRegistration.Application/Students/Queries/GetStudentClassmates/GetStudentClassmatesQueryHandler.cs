using AcademicRegistration.Application.Common.Errors;
using AcademicRegistration.Application.DTOs;
using AcademicRegistration.Application.Interfaces;

namespace AcademicRegistration.Application.Students.Queries.GetStudentClassmates;

public sealed class GetStudentClassmatesQueryHandler
    : IRequestHandler<GetStudentClassmatesQuery, ErrorOr<IReadOnlyList<ClassmatesBySubjectDto>>>
{
    private readonly IStudentReadRepository _studentReadRepository;

    public GetStudentClassmatesQueryHandler(IStudentReadRepository studentReadRepository)
    {
        _studentReadRepository = studentReadRepository;
    }

    public async Task<ErrorOr<IReadOnlyList<ClassmatesBySubjectDto>>> Handle(
        GetStudentClassmatesQuery request,
        CancellationToken cancellationToken)
    {
        var classmates = await _studentReadRepository.GetClassmatesAsync(request.StudentId, cancellationToken);

        if (classmates is null)
        {
            return RegistrationErrors.Students.NotFound(request.StudentId);
        }

        return classmates.ToList();
    }
}
