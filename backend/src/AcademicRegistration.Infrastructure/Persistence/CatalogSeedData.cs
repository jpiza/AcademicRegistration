using AcademicRegistration.Domain.Entities;
using AcademicRegistration.Domain.ValueObjects;

namespace AcademicRegistration.Infrastructure.Persistence;

internal static class CatalogSeedData
{
    public static readonly Guid ProfessorAnaTorresId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ProfessorCarlosMendezId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ProfessorDianaRojasId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid ProfessorFelipeCastroId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid ProfessorLauraGomezId = Guid.Parse("10000000-0000-0000-0000-000000000005");

    public static IReadOnlyList<Professor> Professors =>
    [
        new(ProfessorAnaTorresId, "Ana Torres"),
        new(ProfessorCarlosMendezId, "Carlos Mendez"),
        new(ProfessorDianaRojasId, "Diana Rojas"),
        new(ProfessorFelipeCastroId, "Felipe Castro"),
        new(ProfessorLauraGomezId, "Laura Gomez")
    ];

    public static IReadOnlyList<Subject> Subjects =>
    [
        new(Guid.Parse("20000000-0000-0000-0000-000000000001"), SubjectCode.Create("MAT101"), "Matematicas", ProfessorAnaTorresId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000002"), SubjectCode.Create("FIS101"), "Fisica", ProfessorAnaTorresId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000003"), SubjectCode.Create("PRG101"), "Programacion", ProfessorCarlosMendezId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000004"), SubjectCode.Create("BDD101"), "Bases de Datos", ProfessorCarlosMendezId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000005"), SubjectCode.Create("HIS101"), "Historia", ProfessorDianaRojasId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000006"), SubjectCode.Create("LIT101"), "Literatura", ProfessorDianaRojasId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000007"), SubjectCode.Create("QUI101"), "Quimica", ProfessorFelipeCastroId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000008"), SubjectCode.Create("BIO101"), "Biologia", ProfessorFelipeCastroId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000009"), SubjectCode.Create("EST101"), "Estadistica", ProfessorLauraGomezId),
        new(Guid.Parse("20000000-0000-0000-0000-000000000010"), SubjectCode.Create("ING101"), "Ingles", ProfessorLauraGomezId)
    ];
}
