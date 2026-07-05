using AcademicRegistration.Application.Common.Errors;
using AcademicRegistration.Domain.Repositories;

namespace AcademicRegistration.Application.Students.Commands.DeleteStudent;

public sealed class DeleteStudentCommandHandler : IRequestHandler<DeleteStudentCommand, ErrorOr<Unit>>
{
    private readonly IStudentRepository _studentRepository;

    public DeleteStudentCommandHandler(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository;
    }

    public async Task<ErrorOr<Unit>> Handle(DeleteStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdAsync(request.StudentId, cancellationToken);

        if (student is null)
        {
            return RegistrationErrors.Students.NotFound(request.StudentId);
        }

        _studentRepository.Remove(student);

        return Unit.Value;
    }
}
