namespace TaskManagerApi.Models
{
    public class GroupMembers
    {
        public int Id { get; set; }

        public int GroupId { get; set; }
        public Group group { get; set; }

        public int UserId { get; set; }
        public User user { get; set; }

        public bool isAdmin { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}