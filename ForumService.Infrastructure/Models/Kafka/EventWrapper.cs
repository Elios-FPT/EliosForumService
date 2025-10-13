namespace ForumService.Infrastructure.Models.Kafka
{
    public class EventWrapper
    {
        public string EventType { get; set; }
        public string ModelType { get; set; }
        public object Payload { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string? CorrelationId { get; set; }
    }
}
