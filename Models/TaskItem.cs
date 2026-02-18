using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaskManagerApi.Models
{
    public class TaskItem
    {
        [Key]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; } = 0;
        public DateTime DateTime { get; set; } = DateTime.Now;
        
        [JsonPropertyName("dueDate")]
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public bool isFavorite { get; set; } = false;
        public bool isDeleted { get; set; } = false;
        public string? TokenId { get; set; }
    }
}
