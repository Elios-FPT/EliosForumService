using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ForumService.Web.Attributes
{
    /// <summary>
    /// Attribute used to check whether the current user is banned from performing this action.
    /// It verifies the user ID from the request header and checks ban status in the database.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CheckBannedUserAttribute : Attribute, IAsyncActionFilter
    {
        
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userIdHeader = context.HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();

            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                // Return 401 Unauthorized response if header is missing or invalid
                context.Result = new ObjectResult(new BaseResponseDto<object>
                {
                    Status = 401,
                    Message = "User not identified or invalid X-Auth-Request-User header.",
                    ResponseData = null
                })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            var banRepository = context.HttpContext.RequestServices.GetService<IGenericRepository<ForumUserBan>>();

            if (banRepository != null)
            {
                var bannedUser = await banRepository.GetOneAsync(
                    filter: b => b.UserId == userId
                              && b.IsActive
                              && (b.BanUntil == null || b.BanUntil > DateTime.UtcNow)
                );

                // If a ban record exists, block the action
                if (bannedUser != null)
                {
                    var message = $"Your account has been restricted from using this feature. Reason: {bannedUser.Reason}.";

                    if (bannedUser.BanUntil.HasValue)
                    {
                        message += $" Ban valid until: {bannedUser.BanUntil.Value:dd/MM/yyyy HH:mm}.";
                    }
                    else
                    {
                        message += " Duration: Permanent.";
                    }

                    context.Result = new ObjectResult(new BaseResponseDto<object>
                    {
                        Status = 403,
                        Message = message,
                        ResponseData = null
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    return; 
                }
            }

            await next();
        }
    }
}
