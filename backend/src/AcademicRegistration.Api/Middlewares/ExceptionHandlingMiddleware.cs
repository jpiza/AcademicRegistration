using AcademicRegistration.Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace AcademicRegistration.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (DomainRuleException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                exception.Message,
                exception.Code);
        }
        catch (ArgumentException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                exception.Message,
                "InvalidArgument");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error no controlado procesando la solicitud.");

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Ocurrio un error inesperado.",
                "UnexpectedError");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string code)
    {
        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{statusCode}",
            Extensions =
            {
                ["code"] = code
            }
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
