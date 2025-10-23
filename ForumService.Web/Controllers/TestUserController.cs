using Microsoft.AspNetCore.Mvc;
using ForumService.Contract.Models;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestUserController : ControllerBase
    {
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public TestUserController(IKafkaProducerRepository<User> producerRepository)
        {
            _producerRepository = producerRepository;
        }

        /// <summary>
        /// Get all user profiles from Utility Service
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAll()
        {
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Getting all user profiles from UTILITY");
                var users = await _producerRepository.ProduceGetAllAsync(
                    DestinationService,
                    ResponseTopic
                );
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Received {users.Count()} user profiles");
                return Ok(users);
            }
            catch (TimeoutException ex)
            {
                return StatusCode(408, new { message = $"Request timeout: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get user profile by ID from Utility Service
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetById(Guid id)
        {
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Getting user profile {id} from UTILITY");
                var user = await _producerRepository.ProduceGetByIdAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );
                if (user == null)
                {
                    return NotFound(new { message = $"User profile with ID {id} not found in Utility Service" });
                }
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Found user profile {id}");
                return Ok(user);
            }
            catch (TimeoutException ex)
            {
                return StatusCode(408, new { message = $"Request timeout: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get current authenticated user's profile from Utility Service
        /// </summary>
        [HttpGet("current")]
        public async Task<ActionResult<User>> GetCurrentUserInfo()
        {
            try
            {
                // Đọc userId trực tiếp từ header X-Auth-Request-User (dựa trên sample request whoami)
                var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
                if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
                {
                    return Unauthorized(new { message = "User not authenticated or invalid/missing X-Auth-Request-User header" });
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Getting current user profile {userId} from UTILITY (from X-Auth-Request-User header)");
                var user = await _producerRepository.ProduceGetByIdAsync(
                    userId,
                    DestinationService,
                    ResponseTopic
                );

                if (user == null)
                {
                    return NotFound(new { message = $"Current user profile with ID {userId} not found in Utility Service" });
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Found current user profile {userId}");
                return Ok(user);
            }
            catch (TimeoutException ex)
            {
                return StatusCode(408, new { message = $"Request timeout: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Create user profile in Utility Service
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<User>> Create([FromBody] CreateUserProfileRequest request)
        {
            try
            {
                var userProfile = new User
                {
                    id = Guid.NewGuid(),
                    firstName = request.FirstName,
                    lastName = request.LastName,
                    dateOfBirth = request.DateOfBirth,
                    gender = request.Gender,
                    avatarUrl = request.AvatarUrl,
                    avatarPrefix = request.AvatarPrefix,
                    avatarFileName = request.AvatarFileName,
                    createdAt = DateTime.Now,
                    updatedAt = DateTime.Now
                };
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Creating user profile in UTILITY");
                var createdUserProfile = await _producerRepository.ProduceCreateAsync(
                    userProfile,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Created user profile {createdUserProfile.id}");
                return CreatedAtAction(nameof(GetById), new { id = createdUserProfile.id }, createdUserProfile);
            }
            catch (TimeoutException ex)
            {
                return StatusCode(408, new { message = $"Request timeout: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Update user profile in Utility Service
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<User>> Update(Guid id, [FromBody] UpdateUserProfileRequest request)
        {
            try
            {
                var existingUserProfile = await _producerRepository.ProduceGetByIdAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );
                if (existingUserProfile == null)
                {
                    return NotFound(new { message = $"User profile with ID {id} not found in Utility Service" });
                }
                var updatedUserProfile = new User
                {
                    id = existingUserProfile.id,
                    firstName = request.FirstName ?? existingUserProfile.firstName,
                    lastName = request.LastName ?? existingUserProfile.lastName,
                    dateOfBirth = request.DateOfBirth ?? existingUserProfile.dateOfBirth,
                    gender = request.Gender ?? existingUserProfile.gender,
                    avatarUrl = request.AvatarUrl ?? existingUserProfile.avatarUrl,
                    avatarPrefix = request.AvatarPrefix ?? existingUserProfile.avatarPrefix,
                    avatarFileName = request.AvatarFileName ?? existingUserProfile.avatarFileName,
                    createdAt = existingUserProfile.createdAt,
                    updatedAt = DateTime.Now
                };
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Updating user profile {id} in UTILITY");
                var result = await _producerRepository.ProduceUpdateAsync(
                    updatedUserProfile,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Updated user profile {id}");
                return Ok(result);
            }
            catch (TimeoutException ex)
            {
                return StatusCode(408, new { message = $"Request timeout: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Delete user profile from Utility Service
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var existingUserProfile = await _producerRepository.ProduceGetByIdAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );
                if (existingUserProfile == null)
                {
                    return NotFound(new { message = $"User profile with ID {id} not found in Utility Service" });
                }
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Deleting user profile {id} from UTILITY");
                await _producerRepository.ProduceDeleteAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Deleted user profile {id}");
                return NoContent();
            }
            catch (TimeoutException ex)
            {
                return StatusCode(408, new { message = $"Request timeout: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }
    }

    // REQUEST MODELS
    public class CreateUserProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? AvatarUrl { get; set; }
        public string? AvatarPrefix { get; set; }
        public string? AvatarFileName { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? AvatarUrl { get; set; }
        public string? AvatarPrefix { get; set; }
        public string? AvatarFileName { get; set; }
    }
}