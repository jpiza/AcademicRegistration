using AcademicRegistration.Application.Students.Commands.CreateStudent;
using AcademicRegistration.Application.Students.Commands.DeleteStudent;
using AcademicRegistration.Application.Students.Commands.UpdateStudent;
using AcademicRegistration.Application.Students.Queries.GetStudentById;
using AcademicRegistration.Application.Students.Queries.GetStudentClassmates;
using AcademicRegistration.Application.Students.Queries.GetStudents;
using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AcademicRegistration.Api.Controllers;

[Route("api/students")]
public sealed class StudentsController : ApiController
{
    private readonly ISender _sender;

    public StudentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var students = await _sender.Send(new GetStudentsQuery(), cancellationToken);
        return Ok(students);
    }

    [HttpGet("{studentId:guid}")]
    public async Task<IActionResult> GetById(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetStudentByIdQuery(studentId), cancellationToken);
        return Match(result, Ok);
    }

    [HttpGet("{studentId:guid}/classmates")]
    public async Task<IActionResult> GetClassmates(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetStudentClassmatesQuery(studentId), cancellationToken);
        return Match(result, Ok);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return Match(
            result,
            studentId => CreatedAtAction(nameof(GetById), new { studentId }, new { id = studentId }));
    }

    [HttpPut("{studentId:guid}")]
    public async Task<IActionResult> Update(
        Guid studentId,
        [FromBody] UpdateStudentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.StudentId != studentId)
        {
            return ProblemFromErrors(new List<Error>
            {
                Error.Validation(
                    "Student.UpdateInvalid",
                    "El id de la solicitud no coincide con el id de la URL.")
            });
        }

        var result = await _sender.Send(command, cancellationToken);

        return Match(result, _ => NoContent());
    }

    [HttpDelete("{studentId:guid}")]
    public async Task<IActionResult> Delete(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteStudentCommand(studentId), cancellationToken);
        return Match(result, _ => NoContent());
    }
}
