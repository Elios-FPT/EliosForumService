using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Cache
{
    public static class Request
    {
        public record SetCacheRequest(Guid Key, string Value);
    }
}
