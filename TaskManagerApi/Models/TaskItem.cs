using System.ComponentModel.DataAnnotations;

namespace TaskManagerApi.Models
{
    public class TaskItem
    {
        [Key]
        public int Id { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }

        public bool IsCompleted { get; set; }
        public DateTime DateTime { get; set; } = DateTime.Now;

        public string? TokenId { get; set; }
    }
}
