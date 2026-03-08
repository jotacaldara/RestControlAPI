namespace RestControlAPI.DTOs
{
    public class ReviewDTO
    {

        public int ReviewId { get; set; }
        public string UserName { get; set; }
            public int Rating { get; set; }
            public string? Comment { get; set; }
            public string? Date { get; set; }

        public string? Reply { get; set; }
        public DateTime? RepliedAt { get; set; }

    }
    public class ReplyReviewDTO
    {
        public string Reply { get; set; }
    }
}
