namespace ForumService.Contract.TransferObjects
{
    public class EmailDataDto
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public DateTime ReceivedDate { get; set; }
        public string From { get; set; }
        public string To { get; set; }
    }
}
