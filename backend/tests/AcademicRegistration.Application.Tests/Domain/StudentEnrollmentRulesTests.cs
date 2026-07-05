using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.Primitives;
using AcademicRegistration.Domain.ValueObjects;

namespace AcademicRegistration.Application.Tests.Domain;

public sealed class StudentEnrollmentRulesTests
{
    private static readonly Guid ProfessorOneId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProfessorTwoId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid ProfessorThreeId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    [Fact]
    public void Register_WithThreeSubjectsFromDifferentProfessors_CreatesStudent()
    {
        var subjects = new[]
        {
            Subject("MAT101", "Matematicas", ProfessorOneId),
            Subject("PRG101", "Programacion", ProfessorTwoId),
            Subject("HIS101", "Historia", ProfessorThreeId)
        };

        var student = Student.Register(
            StudentName.Create("Julio Prueba"),
            EmailAddress.Create("julio.prueba@example.com"),
            DocumentNumber.Create("CC-12345"),
            subjects);

        Assert.Equal(Student.RequiredSubjectCount, student.Enrollments.Count);
        Assert.Equal(9, subjects.Sum(subject => subject.Credits.Value));
    }

    [Fact]
    public void Register_WithTwoSubjectsFromSameProfessor_ThrowsDomainRuleException()
    {
        var subjects = new[]
        {
            Subject("MAT101", "Matematicas", ProfessorOneId),
            Subject("FIS101", "Fisica", ProfessorOneId),
            Subject("HIS101", "Historia", ProfessorThreeId)
        };

        var exception = Assert.Throws<DomainRuleException>(() =>
            Student.Register(
                StudentName.Create("Julio Prueba"),
                EmailAddress.Create("julio.prueba@example.com"),
                DocumentNumber.Create("CC-12345"),
                subjects));

        Assert.Equal("Students.DuplicateProfessor", exception.Code);
    }

    [Fact]
    public void Register_WithLessThanThreeSubjects_ThrowsDomainRuleException()
    {
        var subjects = new[]
        {
            Subject("MAT101", "Matematicas", ProfessorOneId),
            Subject("PRG101", "Programacion", ProfessorTwoId)
        };

        var exception = Assert.Throws<DomainRuleException>(() =>
            Student.Register(
                StudentName.Create("Julio Prueba"),
                EmailAddress.Create("julio.prueba@example.com"),
                DocumentNumber.Create("CC-12345"),
                subjects));

        Assert.Equal("Students.SubjectCount", exception.Code);
    }

    private static Subject Subject(string code, string name, Guid professorId)
    {
        return new Subject(Guid.NewGuid(), SubjectCode.Create(code), name, professorId);
    }
}
