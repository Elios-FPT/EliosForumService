using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Contract.TransferObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Email.Query;

namespace ForumService.Core.Handler.Email.Query
{
    public class GetEmailsQueryHandler : IQueryHandler<GetEmailsQuery, BaseResponseDto<IEnumerable<EmailDataDto>>>
    {
        private readonly IEmailService _emailService;

        public GetEmailsQueryHandler(IEmailService emailService)
        {
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public async Task<BaseResponseDto<IEnumerable<EmailDataDto>>> Handle(GetEmailsQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sender))
            {
                return new BaseResponseDto<IEnumerable<EmailDataDto>>
                {
                    Status = 400,
                    Message = "Sender cannot be null or empty.",
                    ResponseData = null
                };
            }

            try
            {
                var emails = await _emailService.GetEmailsFromSenderAsync(request.Sender);
                var emailDtos = emails.Select(email => new EmailDataDto
                {
                    Subject = email.Subject,
                    Body = email.Body,
                    ReceivedDate = email.ReceivedDate,
                    From = email.From
                }).ToList();

                return new BaseResponseDto<IEnumerable<EmailDataDto>>
                {
                    Status = 200,
                    Message = emails.Any() ? "Emails retrieved successfully." : "No emails found.",
                    ResponseData = emailDtos
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<EmailDataDto>>
                {
                    Status = 500,
                    Message = $"Failed to retrieve emails: {ex.Message}",
                    ResponseData = null
                };
            }
        }
    }
}