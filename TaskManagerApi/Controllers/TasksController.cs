using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManagerApi.Data;
using TaskManagerApi.Models;

namespace TaskManagerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        // Dependency injection (Bağımlılık enjeksiyonu) kullanarak AppDbContext'i alıyoruz. Bunun sayesinde 
        // AppDbContext'i yönetmek daha kolay hale gelir ve test edilebilirlik artar.
        private readonly AppDbContext _dbContext;

        public TasksController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private string GetUserIdFromHeader()
        {
            // Frontend'den "user-id" adında bir başlık bekliyoruz
            if (Request.Headers.TryGetValue("user-id", out var userId)) return userId.ToString();

            return "unknown"; // Eğer gelmezse (Test yaparken vs.)
        }

        // HTTP GET isteği için bir endpoint tanımlıyoruz. Bu endpoint veritabanındaki tüm görevleri alır ve döndürür.
        [HttpGet]
        public IActionResult GetTasks([FromQuery] string? search, [FromQuery] string? status)
        {
            var currentUserId = GetUserIdFromHeader();

            // Build the IQueryable and apply filters before materializing the result.
            var query = _dbContext.TaskItems.Where(t => t.TokenId == currentUserId);

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(t => (t.Title != null && t.Title.ToLower().Contains(searchLower)) ||
                (t.Description != null && t.Description.ToLower().Contains(searchLower)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                var filterStatus = status.ToLower();
                if (filterStatus == "completed")
                {
                    query = query.Where(t => t.IsCompleted == true);
                }
                else if (filterStatus == "active")
                {
                    query = query.Where(t => t.IsCompleted == false);
                }
            }

            var tasks = query.OrderByDescending(t => t.Id).ToList();
            return Ok(tasks);
        }

        // HTTP POST isteği için bir endpoint tanımlıyoruz.
        // Bu endpoint istek gövdesinden bir TaskItem nesnesi alır veritabanına ekler ve kaydeder.
        [HttpPost]
        public IActionResult CreateTask([FromBody] TaskItem task)
        {
            if (task == null) return BadRequest();

            task.TokenId = GetUserIdFromHeader();

            if (task.DateTime == default) task.DateTime = DateTime.UtcNow;

            try
            {
                _dbContext.TaskItems.Add(task);
                _dbContext.SaveChanges();
                return CreatedAtAction(nameof(GetTasks), new { id = task.Id }, task);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error creating task: {ex.Message}");
            }
        }

        // HTTP PUT isteği için bir endpoint tanımlıyoruz.
        // Bu endpoint belirtilen Id'ye sahip bir görevi günceller.
        [HttpPut("{id}")]
        public IActionResult UpdateTask(int id, [FromBody] TaskItem UpdatedTask)
        {
            var userId = GetUserIdFromHeader();
            var task = _dbContext.TaskItems.FirstOrDefault(t => t.Id == id && t.TokenId == userId);

            if (task != null)
            {
                try
                {
                    task.Title = UpdatedTask.Title;
                    task.Description = UpdatedTask.Description;
                    task.IsCompleted = UpdatedTask.IsCompleted;

                    _dbContext.SaveChanges();
                    return Ok(task);
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Error updating task: {ex.Message}");
                }
            }

            return NotFound();
        }

        // HTTP DELETE isteği için bir endpoint tanımlıyoruz
        // Bu endpoint belirtilen Id'ye sahip bir görevi siler.
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
