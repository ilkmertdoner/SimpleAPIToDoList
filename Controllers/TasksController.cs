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
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                DueDate = t.DueDate,
                IsCompleted = t.IsCompleted,
                IsFavorite = t.isFavorite,
                IsDeleted = t.isDeleted,
                TokenId = t.TokenId,
                GoogleCalendarEventId = t.GoogleCalendarEventId,
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
                if (task.DueDate.HasValue && user != null &&
                    user.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var userEmail = user.Email;
                        string safeDescription = task.Description ?? "";
                        string eventId = await _calendarService.AddTaskToUserCalendarAsync
                            (userEmail, task.Title, safeDescription, task.DueDate.Value);

                        task.GoogleCalendarEventId = eventId;
                    }
                    catch
                    {
                    }
                }

                await _dbContext.TaskItems.AddAsync(task);
                await _dbContext.SaveChangesAsync();
                return CreatedAtAction(nameof(GetTasks), new { id = task.Id }, task);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskItem updatedTask)
        {
            var userIdString = GetUserIdFromHeader();
            var existingTask = await _dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == id && 
                t.TokenId == userIdString);

            if (existingTask == null) return NotFound();

            existingTask.Title = updatedTask.Title;
            existingTask.Description = updatedTask.Description;
            existingTask.IsCompleted = updatedTask.IsCompleted;
            existingTask.isFavorite = updatedTask.isFavorite;
            existingTask.Priority = updatedTask.Priority;
            existingTask.DueDate = updatedTask.DueDate;

            var user = await _dbContext.Users.FindAsync(int.Parse(userIdString));

            if (user != null && !string.IsNullOrEmpty(user.Email) && user.Email.EndsWith("@gmail.com",
                StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(existingTask.GoogleCalendarEventId))
                {
                    if (existingTask.DueDate.HasValue)
                    {
                        try
                        {
                            await _calendarService.UpdateTaskInUserCalendarAsync(user.Email, existingTask.GoogleCalendarEventId, 
                                existingTask.Title, existingTask.Description ?? "", existingTask.DueDate.Value);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        try
                        {
                            await _calendarService.DeleteTaskFromUserCalendarAsync(user.Email,
                                existingTask.GoogleCalendarEventId);
                        }
                        catch
                        {
                        }
                        existingTask.GoogleCalendarEventId = null;
                    }
                }
                else if (existingTask.DueDate.HasValue)
                {
                    try
                    {
                        string eventId = await _calendarService.AddTaskToUserCalendarAsync(user.Email,
                            existingTask.Title, existingTask.Description ?? "", existingTask.DueDate.Value);

                        existingTask.GoogleCalendarEventId = eventId;
                    }
                    catch
                    {
                    }
                }
            }

            await _dbContext.SaveChangesAsync();
            return Ok(existingTask);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var userIdString = GetUserIdFromHeader();
            var task = await _dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.TokenId == userIdString);

            if (task == null) return NotFound();

            var user = await _dbContext.Users.FindAsync(int.Parse(userIdString));

            if (user != null && !string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(task.GoogleCalendarEventId))
            {
                try
                {
                    await _calendarService.DeleteTaskFromUserCalendarAsync(user.Email, task.GoogleCalendarEventId);
                }
                catch
                {
                }
            }

            _dbContext.TaskItems.Remove(task);
            await _dbContext.SaveChangesAsync();

            return Ok();
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