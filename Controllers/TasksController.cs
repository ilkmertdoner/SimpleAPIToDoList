using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskManagerApi.Data;
using TaskManagerApi.Models;

namespace TaskManagerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public TasksController(AppDbContext dbContext) { _dbContext = dbContext; }

        private string GetUserIdFromHeader()
        {
            if (Request.Headers.TryGetValue("user-id", out var userId)) return userId.ToString();

            return "unknown";
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] string? search, [FromQuery] string? status,
            [FromQuery] int? priority, [FromQuery] DateTime? dueDate)
        {
            var currentUserId = GetUserIdFromHeader();

            var query = _dbContext.TaskItems.Where(t => t.TokenId == currentUserId && !t.isDeleted);

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(t => (t.Title != null && t.Title.ToLower().Contains(searchLower)) ||
                (t.Description != null && t.Description.ToLower().Contains(searchLower)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                var filterStatus = status.ToLower();
                if (filterStatus == "completed") { query = query.Where(t => t.IsCompleted == true); }
                else if (filterStatus == "active") { query = query.Where(t => t.IsCompleted == false); }
            }

            if (priority.HasValue) { query = query.Where(t => t.Priority == priority.Value); }

            if (dueDate.HasValue) { query = query.Where(t => t.DueDate.Value.Date == dueDate.Value.Date); }

            var tasks = await query.OrderByDescending(t => t.Id).ToListAsync();
            return Ok(tasks);
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorite()
        {
            var currentUserId = GetUserIdFromHeader();
            var favoriteTasks =
                await _dbContext.TaskItems.Where(t => t.TokenId == currentUserId && t.isFavorite && t.isDeleted 
                    == false).ToListAsync();

            return Ok(favoriteTasks);
        }

        [HttpPut("favorites/{id}")]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var userId = GetUserIdFromHeader();
            var task = await _dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.TokenId == userId);

            if (task == null) return BadRequest();

            task.isFavorite = !task.isFavorite;
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Favori Durumu Güncellendi.", isFavorite = task.isFavorite });
        }

        [HttpPut("bin/{id}")]
        public async Task<IActionResult> ChangeDeletedTaskStatus(int id)
        {
            var userId = GetUserIdFromHeader();
            var task = await _dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.TokenId == userId);

            if (task == null) return BadRequest();

            task.isDeleted = !task.isDeleted;
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = task.isDeleted ? "Görev Çöpe Atıldı." : "Görev Geri Yüklendi.",
                isDeleted = task.isDeleted
            });
        }

        [HttpGet("bin")]
        public async Task<IActionResult> GetDeletedTasks()
        {
            var currentUserId = GetUserIdFromHeader();
            var deletedTasks = await _dbContext.TaskItems.Where(t => t.TokenId == currentUserId &&
                t.isDeleted).ToListAsync();

            return Ok(deletedTasks);
        }

        [HttpPost]
        public async Task<IActionResult>CreateTask([FromBody] TaskItem task)
        {
            if (task == null) return BadRequest();

            task.TokenId = GetUserIdFromHeader();

            if (task.DateTime == default) task.DateTime = DateTime.UtcNow;

            try
            {
                await _dbContext.TaskItems.AddAsync(task);
                await _dbContext.SaveChangesAsync();
                return CreatedAtAction(nameof(GetTasks), new { id = task.Id }, task);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error creating task: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskItem UpdatedTask)
        {
            var userId = GetUserIdFromHeader();
            var task = await _dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.TokenId == userId);

            if (task != null)
            {
                try
                {
                    task.Title = UpdatedTask.Title;
                    task.Description = UpdatedTask.Description;
                    task.IsCompleted = UpdatedTask.IsCompleted;
                    task.DueDate = UpdatedTask.DueDate;
                    task.Priority = UpdatedTask.Priority;

                    await _dbContext.SaveChangesAsync();
                    return Ok(task);
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Error updating task: {ex.Message}");
                }
            }

            return NotFound();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteTask(int id)
        {
            var userId = GetUserIdFromHeader();
            var task = _dbContext.TaskItems.FirstOrDefault(t => t.Id == id && t.TokenId == userId);

            if (task != null)
            {
                try
                {
                    _dbContext.TaskItems.Remove(task);
                    _dbContext.SaveChanges();

                    return NoContent();
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Error deleting task: {ex.Message}");
                }
            }

            return NotFound();
        }
    }
}
