using ForumService.Contract.Shared; 
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;

namespace ForumService.Web.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class ServiceAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _requiredRoles;
        private const string HeaderName = "X-Auth-Request-Groups";

        public ServiceAuthorizeAttribute(params string[] requiredRoles)
        {
            _requiredRoles = requiredRoles
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .Select(r => r.StartsWith("role:") ? r : $"role:{r}")
                .ToArray();
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (_requiredRoles.Length == 0)
            {
                context.Result = new ObjectResult(new BaseResponseDto<object>
                {
                    Status = 500,
                    Message = "Internal Server Error: No roles configured for this attribute.",
                    ResponseData = null
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
                return;
            }

            var headerValue = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrWhiteSpace(headerValue))
            {
                context.Result = new ObjectResult(new BaseResponseDto<object>
                {
                    Status = 401,
                    Message = $"Unauthorized: Missing required authentication header '{HeaderName}'.",
                    ResponseData = null
                })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            var userRoles = headerValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(r => r.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool hasRequiredRole = _requiredRoles.Any(required =>
                userRoles.Contains(required, StringComparer.OrdinalIgnoreCase));

            if (!hasRequiredRole)
            {
                var requiredRolesClean = string.Join(", ", _requiredRoles.Select(r => r.Replace("role:", "")));

                context.Result = new ObjectResult(new BaseResponseDto<object>
                {
                    Status = 403,
                    Message = $"Access denied. You need one of the following roles: {requiredRolesClean}",
                    ResponseData = null
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }
    }
}