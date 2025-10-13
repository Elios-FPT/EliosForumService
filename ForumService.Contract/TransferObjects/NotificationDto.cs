namespace ForumService.Contract.TransferObjects
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string Url { get; set; }
        public string? Metadata { get; set; }
    }
}
