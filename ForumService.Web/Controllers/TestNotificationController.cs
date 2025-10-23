using ForumService.Contract.Models;
using ForumService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ForumService.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestNotificationController : ControllerBase
    {
        private readonly IKafkaProducerRepository<Notification> _producerRepository;

        private const string ResponseTopic = "utility-forum-notification";
        private const string DestinationService = "utility";

        public TestNotificationController(IKafkaProducerRepository<Notification> producerRepository)
        {
            _producerRepository = producerRepository;
        }

        /// <summary>
        /// Get all notifications from Utility Service
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetAll()
        {
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Getting all notifications from UTILITY");

                var notifications = await _producerRepository.ProduceGetAllAsync(
                    DestinationService,
                    ResponseTopic
                );

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Received {notifications.Count()} notifications");
                return Ok(notifications);
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
        /// Get notification by ID from Utility Service
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Notification>> GetById(Guid id)
        {
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Getting notification {id} from UTILITY");

                var notification = await _producerRepository.ProduceGetByIdAsync(
                    id,
                    DestinationService,           // utility
                    ResponseTopic,                // utility-user-notification
                    cancellationToken: HttpContext.RequestAborted
                );

                if (notification == null)
                {
                    return NotFound(new { message = $"Notification with ID {id} not found in Utility Service" });
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Found notification {id}");
                return Ok(notification);
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
        /// Create notification in Utility Service
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Notification>> Create([FromBody] CreateNotificationRequest request)
        {
            try
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    Title = request.Title,
                    Message = request.Message,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    Url = request.Url,
                    Metadata = request.Metadata
                };

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Creating notification in UTILITY");

                var createdNotification = await _producerRepository.ProduceCreateAsync(
                    notification,
                    DestinationService,           // utility
                    ResponseTopic,                // utility-user-notification
                    cancellationToken: HttpContext.RequestAborted
                );

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Created notification {createdNotification.Id}");
                return CreatedAtAction(nameof(GetById), new { id = createdNotification.Id }, createdNotification);
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
        /// Update notification in Utility Service
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<Notification>> Update(Guid id, [FromBody] UpdateNotificationRequest request)
        {
            try
            {
                var existingNotification = await _producerRepository.ProduceGetByIdAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );

                if (existingNotification == null)
                {
                    return NotFound(new { message = $"Notification with ID {id} not found in Utility Service" });
                }

                existingNotification.Title = request.Title ?? existingNotification.Title;
                existingNotification.Message = request.Message ?? existingNotification.Message;
                existingNotification.IsRead = request.IsRead;
                existingNotification.Url = request.Url ?? existingNotification.Url;
                existingNotification.Metadata = request.Metadata ?? existingNotification.Metadata;

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Updating notification {id} in UTILITY");

                var updatedNotification = await _producerRepository.ProduceUpdateAsync(
                    existingNotification,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Updated notification {id}");
                return Ok(updatedNotification);
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
        /// Delete notification from Utility Service
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var existingNotification = await _producerRepository.ProduceGetByIdAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );

                if (existingNotification == null)
                {
                    return NotFound(new { message = $"Notification with ID {id} not found in Utility Service" });
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Deleting notification {id} from UTILITY");

                await _producerRepository.ProduceDeleteAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Deleted notification {id}");
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

        /// <summary>
        /// Mark notification as read in Utility Service
        /// </summary>
        [HttpPatch("{id}/read")]
        public async Task<ActionResult<Notification>> MarkAsRead(Guid id)
        {
            try
            {
                var notification = await _producerRepository.ProduceGetByIdAsync(
                    id,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );

                if (notification == null)
                {
                    return NotFound(new { message = $"Notification with ID {id} not found in Utility Service" });
                }

                notification.IsRead = true;

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Marking notification {id} as read in UTILITY");

                var updatedNotification = await _producerRepository.ProduceUpdateAsync(
                    notification,
                    DestinationService,
                    ResponseTopic,
                    cancellationToken: HttpContext.RequestAborted
                );

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [USER] Marked notification {id} as read");
                return Ok(updatedNotification);
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

    // REQUEST MODELS (KHÔNG ĐỔI)
    public class CreateNotificationRequest
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? Metadata { get; set; }
    }

    public class UpdateNotificationRequest
    {
        public string? Title { get; set; }
        public string? Message { get; set; }
        public bool IsRead { get; set; }
        public string? Url { get; set; }
        public string? Metadata { get; set; }
    }
}
