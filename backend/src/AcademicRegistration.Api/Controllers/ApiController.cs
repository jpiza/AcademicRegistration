using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcademicRegistration.Api.Controllers;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected IActionResult Match<TValue>(ErrorOr<TValue> result, Func<TValue, IActionResult> onValue)
    {
        return result.IsError ? ProblemFromErrors(result.Errors) : onValue(result.Value);
    }

    protected IActionResult ProblemFromErrors(List<Error> errors)
    {
        if (errors.All(error => error.Type == ErrorType.Validation))
        {
            var modelState = new ModelStateDictionary();

            foreach (var error in errors)
            {
                modelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(modelState);
        }

        var firstError = errors[0];
        var statusCode = firstError.Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(
            statusCode: statusCode,
            title: firstError.Description,
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = errors.Select(error => new { error.Code, error.Description })
            });
    }
}
