using AcademicRegistration.Api.Contracts;
using AcademicRegistration.Application.Students.Commands.CreateStudent;
using AcademicRegistration.Application.Students.Commands.DeleteStudent;
using AcademicRegistration.Application.Students.Commands.UpdateStudent;
using AcademicRegistration.Application.Students.Queries.GetStudentById;
using AcademicRegistration.Application.Students.Queries.GetStudentClassmates;
using AcademicRegistration.Application.Students.Queries.GetStudents;
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
    public async Task<IActionResult> Create(CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateStudentCommand(request.Name, request.Email, request.DocumentNumber, request.SubjectIds),
            cancellationToken);

        return Match(
            result,
            studentId => CreatedAtAction(nameof(GetById), new { studentId }, new { id = studentId }));
    }

    [HttpPut("{studentId:guid}")]
    public async Task<IActionResult> Update(
        Guid studentId,
        UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateStudentCommand(studentId, request.Name, request.Email, request.DocumentNumber, request.SubjectIds),
            cancellationToken);

        return Match(result, _ => NoContent());
    }

    [HttpDelete("{studentId:guid}")]
    public async Task<IActionResult> Delete(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteStudentCommand(studentId), cancellationToken);
        return Match(result, _ => NoContent());
    }
}
