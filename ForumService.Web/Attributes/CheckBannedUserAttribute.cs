using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ForumService.Web.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CheckBannedUserAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userIdHeader = context.HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();

            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
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

                if (bannedUser != null)
                {
                    var message = $"Tài khoản của bạn đã bị khóa tính năng này. Lý do: {bannedUser.Reason}.";
                    if (bannedUser.BanUntil.HasValue)
                    {
                        message += $" Thời hạn đến: {bannedUser.BanUntil.Value:dd/MM/yyyy HH:mm}";
                    }
                    else
                    {
                        message += " Thời hạn: Vĩnh viễn.";
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
