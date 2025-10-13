using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Email
{
    public static class Request
    {
        public record SendEmailRequest(
            string To,
            string Subject,
            string Body
        );
    }
}
