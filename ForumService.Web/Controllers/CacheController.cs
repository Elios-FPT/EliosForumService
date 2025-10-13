using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForumService.Contract.Shared;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Cache.Command;
using static ForumService.Contract.UseCases.Cache.Query;
using static ForumService.Contract.UseCases.Cache.Request;

namespace ForumService.Web.Controllers
{
    /// <summary>
    /// Cache management endpoints.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Cache")]
    [Route("api/v1/[controller]")]
    public class CacheController : ControllerBase
    {
        private readonly ISender _sender;

        public CacheController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Retrieves a cached value by key.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `cache:read` permission to retrieve a cached value by its key.
        /// If the key does not exist, a null value will be returned in the response.
        /// </pre>
        /// </remarks>
        /// <param name="request">A <see cref="GetCacheRequest"/> object containing the cache key.</param>
        /// <returns>
        /// → <seealso cref="GetCacheQuery" /><br/>
        /// → <seealso cref="GetCacheQueryHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing the cached value or null if not found.<br/>
        /// </returns>
        /// <response code="200">Cache value retrieved successfully.</response>
        /// <response code="400">The request is invalid (e.g., missing or invalid key).</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpGet("get")]
        [ProducesResponseType(typeof(BaseResponseDto<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<string>> Get([FromQuery, Required] Guid key)
        {
            var query = new GetCacheQuery(Key: key);
            return await _sender.Send(query);
        }

        /// <summary>
        /// Sets a value in the cache.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `cache:write` permission to set a value in the cache.
        /// </pre>
        /// </remarks>
        /// <param name="request">A <see cref="SetCacheRequest"/> object containing the key and value to cache.</param>
        /// <returns>
        /// → <seealso cref="SetCacheCommand" /><br/>
        /// → <seealso cref="SetCacheCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">Cache value set successfully.</response>
        /// <response code="400">The request is invalid.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpPost("set")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<bool>> Set([FromBody] SetCacheRequest request)
        {
            var command = new SetCacheCommand(Key: request.Key, Value: request.Value);
            return await _sender.Send(command);
        }

        /// <summary>
        /// Removes a cached value by key.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `cache:delete` permission to remove a cached value by its key.
        /// </pre>
        /// </remarks>
        /// <param name="request">A <see cref="RemoveCacheRequest"/> object containing the cache key.</param>
        /// <returns>
        /// → <seealso cref="RemoveCacheCommand" /><br/>
        /// → <seealso cref="RemoveCacheCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        /// <response code="200">Cache value removed successfully.</response>
        /// <response code="400">The request is invalid (e.g., missing or invalid key).</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user does not have permission to access this resource.</response>
        /// <response code="500">An internal server error occurred.</response>
        [HttpDelete("remove")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<bool>> Remove([FromQuery, Required] Guid key)
        {
            var command = new RemoveCacheCommand(Key: key);
            return await _sender.Send(command);
        }
    }
}