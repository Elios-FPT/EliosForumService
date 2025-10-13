using ForumService.Contract.TransferObjects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    public interface IEmailService
    {
        Task<IEnumerable<EmailDataDto>> GetEmailsFromSenderAsync(string sender);
        Task SendEmailAsync(string to, string subject, string body);
    }
}