using MediatR;
using ForumService.Contract.Shared;

namespace ForumService.Contract.Message
{
    public interface ICommand<TResponse> : IRequest<TResponse>;
}
