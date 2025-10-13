using MediatR;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ForumService.Contract.UseCases.Cache
{
    public static class Command
    {
        public record SetCacheCommand(Guid Key, string Value) : ICommand<BaseResponseDto<bool>>;

        public record RemoveCacheCommand(Guid Key) : ICommand<BaseResponseDto<bool>>;
    }
}
