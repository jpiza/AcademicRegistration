namespace AcademicRegistration.Application.Abstractions.Messaging;

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
