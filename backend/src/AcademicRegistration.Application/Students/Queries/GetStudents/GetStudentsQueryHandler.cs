using AcademicRegistration.Application.DTOs;
using AcademicRegistration.Application.Interfaces;

namespace AcademicRegistration.Application.Students.Queries.GetStudents;

public sealed class GetStudentsQueryHandler : IRequestHandler<GetStudentsQuery, IReadOnlyList<StudentSummaryDto>>
{
    private readonly IStudentReadRepository _studentReadRepository;

    public GetStudentsQueryHandler(IStudentReadRepository studentReadRepository)
    {
        _studentReadRepository = studentReadRepository;
    }

    public Task<IReadOnlyList<StudentSummaryDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        return _studentReadRepository.ListAsync(cancellationToken);
    }
}
