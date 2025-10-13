using MediatR;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ForumService.Contract.UseCases.Email
{
    public static class Command
    {
        public record SendEmailCommand(
            string To,
            string Subject,
            string Body
        ) : ICommand<BaseResponseDto<bool>>;
    }
}
