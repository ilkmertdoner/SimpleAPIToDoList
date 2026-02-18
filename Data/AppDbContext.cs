using Microsoft.EntityFrameworkCore;

namespace TaskManagerApi.Data
{
    public class AppDbContext : DbContext
    {
        // Db Context'in constructor'u options parametresi alır ve base sınıfına iletir
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        
        // DbSet veritabanında bir tabloyu temsil eder. TaskItem modeli için bir DbSet tanımlanır
        public DbSet<Models.TaskItem> TaskItems { get; set; }
        public DbSet<Models.User> Users { get; set; }
    }
}
