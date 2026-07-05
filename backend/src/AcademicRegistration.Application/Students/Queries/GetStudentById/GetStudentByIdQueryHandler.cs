using AcademicRegistration.Application.Common.Errors;
using AcademicRegistration.Application.DTOs;
using AcademicRegistration.Application.Interfaces;

namespace AcademicRegistration.Application.Students.Queries.GetStudentById;

public sealed class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, ErrorOr<StudentDetailsDto>>
{
    private readonly IStudentReadRepository _studentReadRepository;

    public GetStudentByIdQueryHandler(IStudentReadRepository studentReadRepository)
    {
        _studentReadRepository = studentReadRepository;
    }

    public async Task<ErrorOr<StudentDetailsDto>> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        var student = await _studentReadRepository.GetByIdAsync(request.StudentId, cancellationToken);

        if (student is null)
        {
            return RegistrationErrors.Students.NotFound(request.StudentId);
        }

        return student;
    }
}
