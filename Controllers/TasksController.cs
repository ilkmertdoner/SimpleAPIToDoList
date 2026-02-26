using System.Security.Claims;
using System.Text;
using Google.Apis.Calendar.v3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskManagerApi.Data;
using TaskManagerApi.Models;
using TaskManagerApi.Service;

namespace TaskManagerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly GoogleCalendarService _calendarService;

        public TasksController(AppDbContext dbContext, GoogleCalendarService calendarService)
        {
            _dbContext = dbContext;
            _calendarService = calendarService;
        }

        private string GetUserIdFromHeader()
        {
            var UserIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (!string.IsNullOrEmpty(UserIdClaim)) return UserIdClaim;

            return "unknown";
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] string search = "", [FromQuery] string status = "all",
            [FromQuery] int? priority = null)
        {
            var currentUserIdStr = GetUserIdFromHeader();
            int currentUserId = int.Parse(currentUserIdStr);

            var query = _dbContext.TaskItems.Where(t => (t.TokenId == currentUserIdStr ||
                t.Assign.Any(x => x.UserId == currentUserId)) && t.isDeleted == false).AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(t => t.Title.Contains(search) || t.Description.Contains(search));

            if (status == "active") query = query.Where(t => t.IsCompleted == false);
            else if (status == "completed") query = query.Where(t => t.IsCompleted == true);

            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            var tasks = await query.Select(t => new
            {
                // automapper kullanılabilir.
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                DueDate = t.DueDate,
                IsCompleted = t.IsCompleted,
                IsFavorite = t.isFavorite,
                IsDeleted = t.isDeleted,
                TokenId = t.TokenId,
                Assign = t.Assign.Select(a => new
                {
                    UserId = a.UserId,
                    User = new
                    {
                        Username = a.User.Username
                    }
                }).ToList()
            }).ToListAsync();

            return Ok(tasks);
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorite()
        {
            var currentUserId = GetUserIdFromHeader();
            var favoriteTasks = await _dbContext.TaskItems.Where(t => t.TokenId == currentUserId && t.isFavorite
                && t.isDeleted == false).ToListAsync();

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
        public async Task<IActionResult> CreateTask([FromBody] TaskItem task)
        {
            if (task == null) return BadRequest();

            task.TokenId = GetUserIdFromHeader();
            var user = await _dbContext.Users.FindAsync(int.Parse(task.TokenId));

            if (task.DateTime == default) task.DateTime = DateTime.UtcNow;

            try
            {
                await _dbContext.TaskItems.AddAsync(task);

                if (task.DueDate.HasValue && user != null && 
                    user.Email.EndsWith("@gmail.com",StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var userEmail = user.Email;
                        await _calendarService.AddTaskToUserCalendarAsync
                            (userEmail, task.Title, task.Description, task.DueDate.Value);
                    }
                    catch
                    {
                    }
                }
                
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
        public async Task<IActionResult> DeleteTask(int id)
        {
            var userId = GetUserIdFromHeader();
            var task = await _dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.TokenId == userId);

            if (task != null)
            {
                try
                {
                    _dbContext.TaskItems.Remove(task);
                    await _dbContext.SaveChangesAsync();

                    return NoContent();
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Error deleting task: {ex.Message}");
                }
            }

            return NotFound();
        }

        [HttpPost("{taskId}/assign/{userId}")]
        public async Task<IActionResult> AssignUserToTask(int taskId, int userId)
        {
            var currentUserIdStr = GetUserIdFromHeader();
            int currentUserId = int.Parse(currentUserIdStr);

            var task = await _dbContext.TaskItems.FindAsync(taskId);
            if (task == null) return NotFound("Görev bulunamadı.");

            if (task.TokenId != currentUserIdStr) return Forbid("Sadece kendi görevlerinize kişi atayabilirsiniz.");

            var targetUser = await _dbContext.Users.FindAsync(userId);
            if (targetUser == null) return NotFound("Kullanıcı bulunamadı.");

            var existingAssign = await _dbContext.TaskAssign
                .FirstOrDefaultAsync(a => a.TaskId == taskId && a.UserId == userId);
            if (existingAssign != null) return BadRequest("Bu kullanıcı zaten bu göreve atanmış.");

            var newAssign = new TaskAssign
            {
                TaskId = taskId,
                UserId = userId
            };
            _dbContext.TaskAssign.Add(newAssign);

            var currentUser = await _dbContext.Users.FindAsync(currentUserId);

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                TokenId = currentUserIdStr,
                Action = "Göreve Ortak Eklendi",
                Details = $"'{task.Title}' adlı göreve @{targetUser.Username} kişisini atadınız."
            });

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                TokenId = targetUser.Id.ToString(),
                Action = "Yeni Görev Ataması",
                Details = $"@{currentUser.Username} sizi '{task.Title}' adlı göreve atadı."
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Kişi göreve başarıyla atandı ve bildirim gönderildi." });
        }

        [HttpDelete("{taskId}/assign/{userId}")]
        public async Task<IActionResult> RemoveUserFromTask(int taskId, int userId)
        {
            var currentUserIdStr = GetUserIdFromHeader();
            int currentUserId = int.Parse(currentUserIdStr);

            var task = await _dbContext.TaskItems.FindAsync(taskId);
            if (task == null) return NotFound("Görev bulunamadı.");

            if (task.TokenId != currentUserIdStr && currentUserId != userId)
                return Forbid("Bu işlem için yetkiniz yok.");

            var assign = await _dbContext.TaskAssign.FirstOrDefaultAsync(a => a.TaskId == taskId && a.UserId == userId);
            if (assign == null) return BadRequest("Kullanıcı bu görevde bulunamadı.");

            _dbContext.TaskAssign.Remove(assign);

            var currentUser = await _dbContext.Users.FindAsync(currentUserId);
            var targetUser = await _dbContext.Users.FindAsync(userId);

            if (currentUserId == userId)
            {
                _dbContext.ActivityLogs.Add(new ActivityLog
                {
                    TokenId = task.TokenId,
                    Action = "Görevden Ayrılma",
                    Details = $"@{currentUser.Username}, '{task.Title}' görevinden ayrıldı."
                });
            }
            else
            {
                _dbContext.ActivityLogs.Add(new ActivityLog
                {
                    TokenId = targetUser.Id.ToString(),
                    Action = "Görevden Çıkarılma",
                    Details = $"@{currentUser.Username}, sizi '{task.Title}' görevinden çıkardı."
                });
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Kullanıcı görevden çıkarıldı." });
        }

        [HttpGet("recent-activities")]
        public async Task<IActionResult> GetRecentActivities()
        {
            var currentUserId = GetUserIdFromHeader();

            var logs = await _dbContext.ActivityLogs
                .Where(l => l.TokenId == currentUserId)
                .OrderByDescending(l => l.Id)
                .Take(50)
                .ToListAsync();

            return Ok(logs);
        }
    }
}