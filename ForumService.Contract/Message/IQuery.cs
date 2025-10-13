using MediatR;
using ForumService.Contract.Shared;

namespace ForumService.Contract.Message
{
    public interface IQuery<TResponse> : IRequest<TResponse>;
}
