using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaskManagerApi.Models
{
    public class TaskItem
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime DateTime { get; set; }
        public bool IsCompleted { get; set; }
        public bool isFavorite { get; set; }
        public bool isDeleted { get; set; }
        public string? TokenId { get; set; }
        public string? GoogleCalendarEventId { get; set; }
        public string? MicrosoftCalendarEventId { get; set; }

        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        [JsonIgnore]
        public ICollection<TaskAssign>? Assign { get; set; }
    }
}
