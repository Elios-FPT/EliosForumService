namespace ForumService.Contract.TransferObjects
{
    public class NotificationDto
    {
        /// <summary>
        /// The ID of the user who will receive the notification.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// A short title for the notification.
        /// </summary>
        public string Title { get; set; } = null!;

        /// <summary>
        /// The main content of the notification.
        /// </summary>
        public string Message { get; set; } = null!;

        /// <summary>
        /// A URL that the notification should link to when clicked.
        /// </summary>
        public string Url { get; set; } = null!;

        /// <summary>
        /// Additional metadata, serialized as a JSON string.
        /// (API expects a string, not an object)
        /// </summary>
        public string Metadata { get; set; } = null!;
    }
}
