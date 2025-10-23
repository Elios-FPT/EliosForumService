using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Models
{
    /// <summary>
    /// Represents an event wrapper for Kafka messages.
    /// </summary>
    public record EventWrapper(string EventType, string ModelType, object Payload, string EventId, string? CorrelationId, DateTime Timestamp = default)
    {
        public EventWrapper() : this(string.Empty, string.Empty, null!, string.Empty, null, DateTime.UtcNow) { }
    }
}
