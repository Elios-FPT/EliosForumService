using MediatR;
using ForumService.Contract.Shared;

namespace ForumService.Contract.Message
{
    public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>;
}
