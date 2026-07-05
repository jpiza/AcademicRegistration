using AcademicRegistration.Application.Subjects.Queries.GetSubjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AcademicRegistration.Api.Controllers;

[Route("api/subjects")]
public sealed class SubjectsController : ApiController
{
    private readonly ISender _sender;

    public SubjectsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var subjects = await _sender.Send(new GetSubjectsQuery(), cancellationToken);
        return Ok(subjects);
    }
}
