namespace AcademicRegistration.Application.Common.Errors;

public static class RegistrationErrors
{
    public static class Students
    {
        public static Error NotFound(Guid studentId) =>
            Error.NotFound("Students.NotFound", $"No existe un estudiante con id '{studentId}'.");

        public static readonly Error EmailAlreadyRegistered =
            Error.Conflict("Students.EmailAlreadyRegistered", "Ya existe un estudiante registrado con ese correo.");

        public static readonly Error DocumentAlreadyRegistered =
            Error.Conflict("Students.DocumentAlreadyRegistered", "Ya existe un estudiante registrado con ese documento.");

        public static readonly Error DuplicateProfessor =
            Error.Validation("Students.DuplicateProfessor", "El estudiante no podra tener clases con el mismo profesor.");
    }

    public static class Subjects
    {
        public static readonly Error SelectionMustHaveThreeSubjects =
            Error.Validation("Subjects.InvalidSelection", "El estudiante debe seleccionar exactamente 3 materias.");

        public static Error NotFound(IReadOnlyCollection<Guid> subjectIds) =>
            Error.NotFound(
                "Subjects.NotFound",
                $"No se encontraron las materias: {string.Join(", ", subjectIds)}.");
    }
}
