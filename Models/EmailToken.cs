using System.ComponentModel.DataAnnotations;

namespace TaskManagerApi.Models
{
    public class EmailToken
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Code { get; set; }
        public DateTime ExpirationDate { get; set; }
    }
}
