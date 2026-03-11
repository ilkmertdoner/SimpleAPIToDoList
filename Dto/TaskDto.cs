namespace TaskManagerApi.Dto
{
    public class TaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public bool isFavorite { get; set; }
        public bool IsDeleted { get; set; }
        public string TokenId { get; set; }
        public string GoogleCalendarEventId { get; set; }
        public string MicrosoftCalendarEventId { get; set; }
        public List<TaskAssignDto> Assign { get; set; }
    }
}
