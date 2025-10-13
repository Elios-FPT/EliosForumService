using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Notification.Command;
using static ForumService.Contract.UseCases.Notification.Query;
using static ForumService.Contract.UseCases.Notification.Request;

namespace ForumService.Web.Controllers
{
    /// <summary>
    /// Notification management endpoints.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Notification")]
    [Route("api/v1/[controller]")]
    public class NotificationController : ControllerBase
    {
        protected readonly ISender Sender;

        public NotificationController(ISender sender)
        {
            Sender = sender;
        }

        /// <summary>
        /// Creates a new notification for a specific user.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `notification:write` permission to create a new notification.
        /// If the user does not have the required permission, a 403 Forbidden response will be returned.
        /// If the request is invalid, a 400 Bad Request response will be returned.
        /// </pre>
        /// </remarks>
        /// <param name="request">
        /// A <see cref="CreateNotificationRequest"/> object containing the properties for the new notification.
        /// </param>
        /// <returns>
        /// → <seealso cref="CreateNotificationCommand" /><br/>
        /// → <seealso cref="CreateNotificationCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">Notification created successfully.</response>
        /// <response code="400">The request is invalid.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<bool>> CreateNotification([FromBody] CreateNotificationRequest request)
        {
            var command = new CreateNotificationCommand(
                UserId: request.UserId,
                Title: request.Title,
                Message: request.Message,
                Url: request.Url,
                Metadata: request.Metadata);

            return await Sender.Send(command);
        }

        /// <summary>
        /// Deletes a specific notification by its unique identifier.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `notification:delete` permission to delete a specific notification by its ID.
        /// If the notification ID does not exist, a 404 Not Found response will be returned.
        /// </pre>
        /// </remarks>
        /// <param name="request">
        /// A <see cref="DeleteNotificationRequest"/> object containing the notification ID.
        /// </param>
        /// <returns>
        /// → <seealso cref="DeleteNotificationCommand" /><br/>
        /// → <seealso cref="DeleteNotificationCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">Notification deleted successfully.</response>
        /// <response code="400">The request is invalid (e.g., invalid notification ID format).</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="404">The specified notification was not found.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpDelete("{notificationId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> DeleteNotification([FromRoute] DeleteNotificationRequest request)
        {
            var command = new DeleteNotificationCommand(NotificationId: request.NotificationId);

            return await Sender.Send(command);
        }

        /// <summary>
        /// Retrieves a paginated list of notifications for a specific user.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `notification:read` permission to fetch a paginated list of notifications for a user.
        /// If no notifications are available, the response will return an empty list.
        /// </pre>
        /// </remarks>
        /// <param name="request">
        /// A <see cref="GetNotificationsRequest"/> object containing pagination parameters and user ID.
        /// </param>
        /// <returns>
        /// → <seealso cref="GetNotificationsQuery" /><br/>
        /// → <seealso cref="GetNotificationsQueryHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a <see cref="PagedResult{NotificationDto}"/> of notifications.<br/>
        /// </returns>
        /// <response code="200">Returns the paginated list of notifications.</response>
        /// <response code="400">The request is invalid (e.g., invalid pagination parameters).</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpGet]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<NotificationDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<IEnumerable<NotificationDto>>> GetNotifications([FromQuery] GetNotificationsRequest request)
        {
            var query = new GetNotificationsQuery(
                UserId: request.UserId,
                UnreadOnly: request.UnreadOnly,
                Limit: request.Limit,
                Offset: request.Offset);

            return await Sender.Send(query);
        }

        /// <summary>
        /// Retrieves the count of unread notifications for a specific user.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `notification:read` permission to fetch the count of unread notifications for a user.
        /// </pre>
        /// </remarks>
        /// <param name="request">
        /// A <see cref="GetUnreadCountRequest"/> object containing the user ID.
        /// </param>
        /// <returns>
        /// → <seealso cref="GetUnreadCountQuery" /><br/>
        /// → <seealso cref="GetUnreadCountQueryHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing an integer representing the unread notification count.<br/>
        /// </returns>
        /// <response code="200">Returns the count of unread notifications.</response>
        /// <response code="400">The request is invalid.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(BaseResponseDto<int>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<int>> GetUnreadCount([FromQuery] GetUnreadCountRequest request)
        {
            var query = new GetUnreadCountQuery(UserId: request.UserId);

            return await Sender.Send(query);
        }

        /// <summary>
        /// Marks a specific notification as read.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `notification:write` permission to mark a specific notification as read.
        /// If the notification ID does not exist, a 404 Not Found response will be returned.
        /// </pre>
        /// </remarks>
        /// <param name="request">
        /// A <see cref="MarkAsReadRequest"/> object containing the notification ID.
        /// </param>
        /// <returns>
        /// → <seealso cref="MarkAsReadCommand" /><br/>
        /// → <seealso cref="MarkAsReadCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">Notification marked as read successfully.</response>
        /// <response code="400">The request is invalid (e.g., invalid notification ID format).</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="404">The specified notification was not found.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpPut("{notificationId}/mark-as-read")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> MarkAsRead([FromRoute] MarkAsReadRequest request)
        {
            var command = new MarkAsReadCommand(NotificationId: request.NotificationId);

            return await Sender.Send(command);
        }

        /// <summary>
        /// Marks all notifications for a specific user as read.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `notification:write` permission to mark all notifications for a user as read.
        /// </pre>
        /// </remarks>
        /// <param name="request">
        /// A <see cref="MarkAllAsReadRequest"/> object containing the user ID.
        /// </param>
        /// <returns>
        /// → <seealso cref="MarkAllAsReadCommand" /><br/>
        /// → <seealso cref="MarkAllAsReadCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">All notifications marked as read successfully.</response>
        /// <response code="400">The request is invalid.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpPut("mark-all-as-read")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<bool>> MarkAllAsRead([FromQuery] MarkAllAsReadRequest request)
        {
            var command = new MarkAllAsReadCommand(UserId: request.UserId);

            return await Sender.Send(command);
        }

        /// <summary>
        /// Clears all notifications for a specific user.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `notification:delete` permission to clear all notifications for a user.
        /// </pre>
        /// </remarks>
        /// <param name="request">
        /// A <see cref="ClearNotificationsRequest"/> object containing the user ID.
        /// </param>
        /// <returns>
        /// → <seealso cref="ClearNotificationsCommand" /><br/>
        /// → <seealso cref="ClearNotificationsCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">All notifications cleared successfully.</response>
        /// <response code="400">The request is invalid.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpDelete("clear")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<bool>> ClearNotifications([FromQuery] ClearNotificationsRequest request)
        {
            var command = new ClearNotificationsCommand(UserId: request.UserId);

            return await Sender.Send(command);
        }
    }
}