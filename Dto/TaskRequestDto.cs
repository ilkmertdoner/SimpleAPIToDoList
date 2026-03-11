namespace TaskManagerApi.Dto
{
    public class TaskRequestDto
    {
        public string Title { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public bool isFavorite { get; set; }
        public int? GroupId { get; set; }
        public string? GoogleCalendarEventId { get; set; }
        public string? MicrosoftCalendarEventId { get; set; }
    }
}
