using AcademicRegistration.Application.Common.Errors;
using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.Repositories;
using AcademicRegistration.Domain.ValueObjects;

namespace AcademicRegistration.Application.Students.Commands.UpdateStudent;

public sealed class UpdateStudentCommandHandler : IRequestHandler<UpdateStudentCommand, ErrorOr<Unit>>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ISubjectRepository _subjectRepository;

    public UpdateStudentCommandHandler(IStudentRepository studentRepository, ISubjectRepository subjectRepository)
    {
        _studentRepository = studentRepository;
        _subjectRepository = subjectRepository;
    }

    public async Task<ErrorOr<Unit>> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdAsync(request.StudentId, cancellationToken);

        if (student is null)
        {
            return RegistrationErrors.Students.NotFound(request.StudentId);
        }

        var email = EmailAddress.Create(request.Email);
        var documentNumber = DocumentNumber.Create(request.DocumentNumber);

        if (await _studentRepository.ExistsByEmailAsync(email.Value, student.Id, cancellationToken))
        {
            return RegistrationErrors.Students.EmailAlreadyRegistered;
        }

        if (await _studentRepository.ExistsByDocumentNumberAsync(documentNumber.Value, student.Id, cancellationToken))
        {
            return RegistrationErrors.Students.DocumentAlreadyRegistered;
        }

        var selectedSubjectsResult = await GetValidatedSubjectsAsync(request.SubjectIds, cancellationToken);

        if (selectedSubjectsResult.IsError)
        {
            return selectedSubjectsResult.Errors;
        }

        student.UpdateProfile(StudentName.Create(request.Name), email, documentNumber);
        student.ChangeEnrollment(selectedSubjectsResult.Value);

        return Unit.Value;
    }

    private async Task<ErrorOr<IReadOnlyList<Subject>>> GetValidatedSubjectsAsync(
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken)
    {
        if (subjectIds.Count != Student.RequiredSubjectCount)
        {
            return RegistrationErrors.Subjects.SelectionMustHaveThreeSubjects;
        }

        var selectedSubjects = await _subjectRepository.GetByIdsAsync(subjectIds, cancellationToken);
        var missingSubjectIds = subjectIds.Except(selectedSubjects.Select(subject => subject.Id)).ToArray();

        if (missingSubjectIds.Length > 0)
        {
            return RegistrationErrors.Subjects.NotFound(missingSubjectIds);
        }

        if (selectedSubjects.Select(subject => subject.ProfessorId).Distinct().Count() != selectedSubjects.Count)
        {
            return RegistrationErrors.Students.DuplicateProfessor;
        }

        return selectedSubjects.ToList();
    }
}
