using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Email.Command;

namespace ForumService.Core.Handler.Email.Command
{
    public class SendEmailCommandHandler : ICommandHandler<SendEmailCommand, BaseResponseDto<bool>>
    {
        private readonly IEmailService _emailService;

        public SendEmailCommandHandler(IEmailService emailService)
        {
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public async Task<BaseResponseDto<bool>> Handle(SendEmailCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.To))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Recipient address cannot be null or empty.",
                    ResponseData = false
                };
            }

            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Subject cannot be null or empty.",
                    ResponseData = false
                };
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Body cannot be null or empty.",
                    ResponseData = false
                };
            }

            try
            {
                await _emailService.SendEmailAsync(request.To, request.Subject, request.Body);
                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Email sent successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to send email: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}