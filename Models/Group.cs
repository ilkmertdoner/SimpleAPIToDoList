namespace TaskManagerApi.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int CreatorId { get; set; }
        public User Creator { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICollection<TaskItem> GroupTasks { get; set; }
        public ICollection<GroupMembers> Members { get; set; }
    }
}
