namespace TaskManagerApi.Dto
{
    public class TaskAssignDto
    {
        public int UserId { get; set; }
        public AssignUserDto User { get; set; }
    }
}