using MediatR;
using ForumService.Contract.Shared;

namespace ForumService.Contract.Message
{
    public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>;
}
