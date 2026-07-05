using AcademicRegistration.Domain.Entities;

namespace AcademicRegistration.Application.Students.Commands.CreateStudent;

public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(120);

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(command => command.DocumentNumber)
            .NotEmpty()
            .Matches("^[A-Za-z0-9-]{5,20}$")
            .WithMessage("El documento solo puede contener letras, numeros o guiones y debe tener entre 5 y 20 caracteres.");

        RuleFor(command => command.SubjectIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(subjectIds => subjectIds.Count == Student.RequiredSubjectCount)
            .WithMessage("El estudiante debe seleccionar exactamente 3 materias.")
            .Must(subjectIds => subjectIds.Distinct().Count() == subjectIds.Count)
            .WithMessage("El estudiante no puede seleccionar la misma materia mas de una vez.");
    }
}
