using AcademicRegistration.Application.DTOs;
using AcademicRegistration.Application.Interfaces;

namespace AcademicRegistration.Application.Subjects.Queries.GetSubjects;

public sealed class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    private readonly ISubjectReadRepository _subjectReadRepository;

    public GetSubjectsQueryHandler(ISubjectReadRepository subjectReadRepository)
    {
        _subjectReadRepository = subjectReadRepository;
    }

    public Task<IReadOnlyList<SubjectDto>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        return _subjectReadRepository.ListAsync(cancellationToken);
    }
}
