using MediatR;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Cache
{
    public static class Query
    {
        public record GetCacheQuery(Guid Key) : IQuery<BaseResponseDto<string>>;
    }
}
