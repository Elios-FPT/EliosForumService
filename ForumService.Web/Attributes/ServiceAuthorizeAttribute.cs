using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
                context.Result = new StatusCodeResult(500);
                return;
            }

            var headerValue = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrWhiteSpace(headerValue))
            {
                context.Result = new ForbidResult($"Missing header: {HeaderName}");
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
                context.Result = new ForbidResult(
                    $"Access denied. Required one of the following roles: {string.Join(", ", _requiredRoles.Select(r => r.Replace("role:", "")))}");
                return;
            }
        }
    }
}
