using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using System.ComponentModel.DataAnnotations;
using static ForumService.Contract.UseCases.Email.Command;
using static ForumService.Contract.UseCases.Email.Query;
using static ForumService.Contract.UseCases.Email.Request;

namespace ForumService.Web.Controllers
{
    /// <summary>
    /// Email management endpoints.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Email")]
    [Route("api/v1/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly ISender _sender;

        public EmailController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Sends an email to the specified recipient.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `email:write` permission to send an email.
        /// </pre>
        /// </remarks>
        /// <param name="request">A <see cref="SendEmailRequest"/> object containing the recipient, subject, and body.</param>
        /// <returns>
        /// → <seealso cref="SendEmailCommand" /><br/>
        /// → <seealso cref="SendEmailCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">Email sent successfully.</response>
        /// <response code="400">The request is invalid (e.g., missing or invalid email parameters).</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpPost("email")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<bool>> SendEmail([FromBody] SendEmailRequest request)
        {
            var command = new SendEmailCommand(To: request.To, Subject: request.Subject, Body: request.Body);
            return await _sender.Send(command);
        }

        /// <summary>
        /// Retrieves emails from a specific sender.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `email:read` permission to retrieve emails from a specific sender.
        /// If no emails are found, an empty list will be returned.
        /// </pre>
        /// </remarks>
        /// <param name="request">A <see cref="GetEmailsRequest"/> object containing the sender's email address.</param>
        /// <returns>
        /// → <seealso cref="GetEmailsQuery" /><br/>
        /// → <seealso cref="GetEmailsQueryHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a list of emails.<br/>
        /// </returns>
        /// <response code="200">Emails retrieved successfully.</response>
        /// <response code="400">The request is invalid (e.g., missing or invalid sender).</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpGet("emails")]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<EmailDataDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<IEnumerable<EmailDataDto>>> GetEmails([FromQuery, Required] string sender)
        {
            var query = new GetEmailsQuery(Sender: sender);
            return await _sender.Send(query);
        }
    }
}
