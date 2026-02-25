using Microsoft.EntityFrameworkCore;
using TaskManagerApi.Models;

namespace TaskManagerApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<FriendSystem> FriendSystem { get; set; }
        public DbSet<TaskAssign> TaskAssign { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<EmailToken> EmailTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaskAssign>()
                .HasKey(ta => new { ta.TaskId, ta.UserId });

            modelBuilder.Entity<TaskAssign>()
                .HasOne(ta => ta.Task)
                .WithMany(t => t.Assign)
                .HasForeignKey(ta => ta.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskAssign>()
                .HasOne(ta => ta.User)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(ta => ta.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FriendSystem>()
                .HasOne(f => f.Requester)
                .WithMany()
                .HasForeignKey(f => f.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FriendSystem>()
                .HasOne(f => f.Receiver)
                .WithMany()
                .HasForeignKey(f => f.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}