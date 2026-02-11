using System.ComponentModel.DataAnnotations;

namespace TaskManagerApi.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; } 

        public DateTime CreationTime { get; set; } = DateTime.Now;
    }
}
