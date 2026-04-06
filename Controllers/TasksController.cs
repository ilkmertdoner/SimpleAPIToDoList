using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagerApi.Data;
using TaskManagerApi.Models;
using TaskManagerApi.Service;
using TaskManagerApi.Dto;
using Mapster;
using Microsoft.AspNetCore.SignalR;

namespace TaskManagerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly GoogleCalendarService _calendarService;
        private readonly MicrosoftCalendarService _microsoftCalendarService;
        private readonly IHubContext<NotificationHub>? _hubContext;
        public TasksController(AppDbContext dbContext, GoogleCalendarService calendarService,
            MicrosoftCalendarService microsoftCalendarService, IHubContext<NotificationHub> hubContext)
        {
            _dbContext = dbContext;
            _calendarService = calendarService;
            _microsoftCalendarService = microsoftCalendarService;
            _hubContext = hubContext;
        }

        private string GetUserIdFromHeader()
        {
            var UserIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (!string.IsNullOrEmpty(UserIdClaim)) return UserIdClaim;
            return "unknown";
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] string search = "", [FromQuery] string status = "all",
            [FromQuery] int? priority = null, [FromQuery] int? groupId = null)
        {
            var currentUserIdStr = GetUserIdFromHeader();
            int currentUserId = int.Parse(currentUserIdStr);

            var query = _dbContext.TaskItems.Where(t => t.isDeleted == false).AsQueryable();

            if (groupId.HasValue)
            {
                var isMember = await _dbContext.GroupMembers
                    .AnyAsync(gm => gm.GroupId == groupId.Value && gm.UserId == currentUserId);

                if (!isMember)
                    return StatusCode(StatusCodes.Status403Forbidden, "Bu grubun görevlerini görme yetkiniz yok.");

                query = query.Where(t => t.GroupId == groupId.Value);
            }
            else
            {
                query = query.Where(t => t.GroupId == null && (t.TokenId == currentUserIdStr ||
                    t.Assign.Any(x => x.UserId == currentUserId)));
            }

            if (!string.IsNullOrEmpty(search))
                query = query.Where(t => t.Title.Contains(search) || t.Description.Contains(search));

            if (status == "active") query = query.Where(t => t.IsCompleted == false);
            else if (status == "completed") query = query.Where(t => t.IsCompleted == true);

            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            var tasks = await query.ProjectToType<TaskDto>().ToListAsync();

            return Ok(tasks);
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorite()
        {
            var currentUserId = GetUserIdFromHeader();
            int userId = int.Parse(currentUserId);

            var query = _dbContext.TaskItems
                .Where(t => t.isFavorite && t.isDeleted == false &&
                    (t.TokenId == currentUserId ||
                     (t.GroupId != null && t.Group.Members.Any(gm => gm.UserId == userId))));

            var favoriteTasks = await query.ProjectToType<TaskDto>().ToListAsync();

            return Ok(favoriteTasks);
        }

        [HttpPut("favorites/{id}")]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var userId = GetUserIdFromHeader();
            int currentUserId = int.Parse(userId);

            var task = await _dbContext.TaskItems.FindAsync(id);
            if (task == null) return NotFound("Görev bulunamadı.");

            bool isPersonalOwner = task.GroupId == null && task.TokenId == userId;
            bool isAssigned = task.GroupId == null && await _dbContext.TaskAssign.AnyAsync
                (a => a.TaskId == id && a.UserId == currentUserId);

            bool isGroupMember = task.GroupId != null && await _dbContext.GroupMembers.AnyAsync
                (gm => gm.GroupId == task.GroupId && gm.UserId == currentUserId);

            if (!isPersonalOwner && !isAssigned && !isGroupMember)
                return StatusCode(StatusCodes.Status403Forbidden, "Yetkiniz yok.");

            task.isFavorite = !task.isFavorite;
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Favori Durumu Güncellendi.", isFavorite = task.isFavorite });
        }

        [HttpPut("bin/{id}")]
        public async Task<IActionResult> ChangeDeletedTaskStatus(int id)
        {
            var userIdString = GetUserIdFromHeader();
            int currentUserId = int.Parse(userIdString);

            var task = await _dbContext.TaskItems.FindAsync(id);
            if (task == null) return NotFound("Görev bulunamadı.");

            bool isPersonalOwner = task.GroupId == null && task.TokenId == userIdString;
            bool isTaskCreator = task.TokenId == userIdString;
            bool isGroupAdmin = task.GroupId != null && await _dbContext.GroupMembers.AnyAsync
                (gm => gm.GroupId == task.GroupId && gm.UserId == currentUserId && gm.isAdmin);

            if (!isPersonalOwner && !isTaskCreator && !isGroupAdmin)
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Sadece görevi oluşturan kişi veya grup yöneticisi çöpe atabilir.");

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
            int userId = int.Parse(currentUserId);

            var query = _dbContext.TaskItems
                .Where(t => t.isDeleted &&
                    (t.TokenId == currentUserId ||
                     (t.GroupId != null && t.Group.Members.Any(gm => gm.UserId == userId))));

            var deletedTasks = await query.ProjectToType<TaskDto>().ToListAsync();

            return Ok(deletedTasks);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] TaskRequestDto dto)
        {
            var userIdStr = GetUserIdFromHeader();
            var currentUserId = int.Parse(userIdStr);

            if (dto.GroupId.HasValue)
            {
                var isMember = await _dbContext.GroupMembers
                    .AnyAsync(gm => gm.GroupId == dto.GroupId.Value && gm.UserId == currentUserId);

                if (!isMember)
                    return StatusCode(StatusCodes.Status403Forbidden, "Bu gruba görev ekleme yetkiniz yok.");
            }

            var user = await _dbContext.Users.FindAsync(currentUserId);

            var newTask = new TaskItem
            {
                Title = dto.Title,
                Description = dto.Description,
                Priority = dto.Priority,
                DueDate = dto.DueDate,
                IsCompleted = dto.IsCompleted,
                isFavorite = dto.isFavorite,
                GroupId = dto.GroupId,
                TokenId = userIdStr,
                DateTime = DateTime.UtcNow,
                isDeleted = false
            };

            try
            {
                if (newTask.DueDate.HasValue && user != null && !string.IsNullOrEmpty(user.Email))
                {
                    if (user.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            newTask.GoogleCalendarEventId = await _calendarService.AddTaskToUserCalendarAsync
                                (user.Email, newTask.Title, newTask.Description ?? "", newTask.DueDate.Value);
                        }
                        catch { }
                    }
                    else if (user.Email.EndsWith("@hotmail.com", StringComparison.OrdinalIgnoreCase)
                        || user.Email.EndsWith("@outlook.com", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            newTask.MicrosoftCalendarEventId = await _microsoftCalendarService
                                .AddTaskToUserCalendarAsync(user.Email, newTask.Title, newTask.Description ?? "",
                                newTask.DueDate.Value);
                        }
                        catch { }
                    }
                }

                _dbContext.TaskItems.Add(newTask);
                await _dbContext.SaveChangesAsync();
                return Ok(newTask);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error creating task: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskRequestDto dto)
        {
            var userIdString = GetUserIdFromHeader();
            int currentUserId = int.Parse(userIdString);

            var existingTask = await _dbContext.TaskItems.FindAsync(id);
            if (existingTask == null) return NotFound("Görev bulunamadı.");

            bool isPersonalOwner = existingTask.GroupId == null && existingTask.TokenId == userIdString;
            bool isAssigned = existingTask.GroupId == null
                && await _dbContext.TaskAssign.AnyAsync(a => a.TaskId == id && a.UserId == currentUserId);

            bool isGroupMember = existingTask.GroupId != null
                && await _dbContext.GroupMembers.AnyAsync(gm => gm.GroupId == existingTask.GroupId
                && gm.UserId == currentUserId);

            if (!isPersonalOwner && !isAssigned && !isGroupMember)
                return StatusCode(StatusCodes.Status403Forbidden, "Bu görevi düzenleme yetkiniz yok.");

            existingTask.Title = dto.Title;
            existingTask.Description = dto.Description;
            existingTask.IsCompleted = dto.IsCompleted;
            existingTask.isFavorite = dto.isFavorite;
            existingTask.Priority = dto.Priority;
            existingTask.DueDate = dto.DueDate;

            var creatorUser = await _dbContext.Users.FindAsync(int.Parse(existingTask.TokenId));

            if (creatorUser != null && !string.IsNullOrEmpty(creatorUser.Email))
            {
                if (creatorUser.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(existingTask.GoogleCalendarEventId))
                    {
                        if (existingTask.DueDate.HasValue)
                        {
                            try
                            {
                                await _calendarService.UpdateTaskInUserCalendarAsync
                                    (creatorUser.Email, existingTask.GoogleCalendarEventId, existingTask.Title,
                                    existingTask.Description ?? "", existingTask.DueDate.Value);
                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                await _calendarService.DeleteTaskFromUserCalendarAsync
                                    (creatorUser.Email, existingTask.GoogleCalendarEventId);
                            }
                            catch { }
                            existingTask.GoogleCalendarEventId = null;
                        }
                    }
                    else if (existingTask.DueDate.HasValue)
                    {
                        try
                        {
                            existingTask.GoogleCalendarEventId = await _calendarService.AddTaskToUserCalendarAsync
                                (creatorUser.Email, existingTask.Title, existingTask.Description ?? "",
                                existingTask.DueDate.Value);
                        }
                        catch { }
                    }
                }
                else if (creatorUser.Email.EndsWith("@hotmail.com", StringComparison.OrdinalIgnoreCase)
                    || creatorUser.Email.EndsWith("@outlook.com", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(existingTask.MicrosoftCalendarEventId))
                    {
                        if (existingTask.DueDate.HasValue)
                        {
                            try
                            {
                                await _microsoftCalendarService.UpdateTaskInUserCalendarAsync
                                    (creatorUser.Email, existingTask.MicrosoftCalendarEventId,
                                    existingTask.Title, existingTask.Description ?? "",
                                    existingTask.DueDate.Value);
                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                await _microsoftCalendarService.DeleteTaskFromUserCalendarAsync
                                    (creatorUser.Email, existingTask.MicrosoftCalendarEventId);
                            }
                            catch { }

                            existingTask.MicrosoftCalendarEventId = null;
                        }
                    }
                    else if (existingTask.DueDate.HasValue)
                    {
                        try
                        {
                            existingTask.MicrosoftCalendarEventId =
                                await _microsoftCalendarService.AddTaskToUserCalendarAsync
                                (creatorUser.Email, existingTask.Title, existingTask.Description ?? "",
                                existingTask.DueDate.Value);
                        }
                        catch { }
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
            int currentUserId = int.Parse(userIdString);

            var task = await _dbContext.TaskItems.FindAsync(id);
            if (task == null) return NotFound("Görev bulunamadı.");

            bool isPersonalOwner = task.GroupId == null && task.TokenId == userIdString;
            bool isTaskCreator = task.TokenId == userIdString;
            bool isGroupAdmin = task.GroupId != null && await _dbContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == task.GroupId && gm.UserId == currentUserId && gm.isAdmin);

            if (!isPersonalOwner && !isTaskCreator && !isGroupAdmin)
                return StatusCode(StatusCodes.Status403Forbidden, "Sadece görevi oluşturan kişi veya grup yöneticisi silebilir.");

            var user = await _dbContext.Users.FindAsync(int.Parse(task.TokenId));

            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                if (user.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(task.GoogleCalendarEventId))
                {
                    try { await _calendarService.DeleteTaskFromUserCalendarAsync(user.Email, task.GoogleCalendarEventId); } catch { }
                }
                else if ((user.Email.EndsWith("@hotmail.com", StringComparison.OrdinalIgnoreCase)
                    || user.Email.EndsWith("@outlook.com", StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrEmpty(task.MicrosoftCalendarEventId))
                {
                    try { await _microsoftCalendarService.DeleteTaskFromUserCalendarAsync(user.Email, task.MicrosoftCalendarEventId); } catch { }
                }
            }

            var assigns = _dbContext.TaskAssign.Where(a => a.TaskId == id);
            _dbContext.TaskAssign.RemoveRange(assigns);

            _dbContext.TaskItems.Remove(task);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Görev başarıyla silindi." });
        }

        [HttpPost("{taskId}/assign/{userId}")]
        public async Task<IActionResult> AssignUserToTask(int taskId, int userId)
        {
            var currentUserIdStr = GetUserIdFromHeader();
            int currentUserId = int.Parse(currentUserIdStr);

            var task = await _dbContext.TaskItems.FindAsync(taskId);
            if (task == null) return NotFound("Görev bulunamadı.");

            if (task.GroupId != null)
                return BadRequest("Grup görevlerine ekstra kişi ataması yapılamaz.");

            bool isPersonalTaskOwner = task.GroupId == null && task.TokenId == currentUserIdStr;

            if (!isPersonalTaskOwner)
                return StatusCode(StatusCodes.Status403Forbidden, "Bu işlem için yetkiniz yok.");

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

            var currentUserLog = new ActivityLog
            {
                TokenId = currentUserIdStr,
                Action = "Göreve Ortak Eklendi",
                Details = $"'{task.Title}' adlı göreve @{targetUser.Username} kişisini atadınız.",
                CreatedAt = DateTime.Now,
            };

            var targetUserLog = new ActivityLog
            {
                TokenId = targetUser.Id.ToString(),
                Action = "Yeni Görev Ataması",
                Details = $"@{currentUser.Username} sizi '{task.Title}' adlı göreve atadı.",
                CreatedAt = DateTime.Now,
            };

            _dbContext.ActivityLogs.Add(currentUserLog);
            _dbContext.ActivityLogs.Add(targetUserLog);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(currentUserIdStr)
                .SendAsync("ReceiveLog", currentUserLog.Action, currentUserLog.Details);

            await _hubContext.Clients.User(targetUser.Id.ToString())
                .SendAsync("ReceiveLog", targetUserLog.Action, targetUserLog.Details);

            return Ok(new { message = "Kişi göreve başarıyla atandı ve bildirim gönderildi." });
        }

        [HttpDelete("{taskId}/assign/{userId}")]
        public async Task<IActionResult> RemoveUserFromTask(int taskId, int userId)
        {
            var currentUserIdStr = GetUserIdFromHeader();
            int currentUserId = int.Parse(currentUserIdStr);

            var task = await _dbContext.TaskItems.FindAsync(taskId);
            if (task == null) return NotFound("Görev bulunamadı.");

            if (task.GroupId != null)
                return BadRequest("Grup görevlerinde atama işlemi yapılamaz.");

            bool isTaskCreator = task.TokenId == currentUserIdStr;
            bool isSelfRemoving = currentUserId == userId;

            if (!isTaskCreator && !isSelfRemoving)
                return StatusCode(StatusCodes.Status403Forbidden, "Bu işlem için yetkiniz yok.");

            var assign = await _dbContext.TaskAssign.FirstOrDefaultAsync(a => a.TaskId == taskId && a.UserId == userId);
            if (assign == null) return BadRequest("Kullanıcı bu görevde bulunamadı.");

            _dbContext.TaskAssign.Remove(assign);

            var currentUser = await _dbContext.Users.FindAsync(currentUserId);
            var targetUser = await _dbContext.Users.FindAsync(userId);

            if (currentUserId == userId)
            {
                var leaveTask = new ActivityLog
                {
                    TokenId = task.TokenId,
                    Action = "Görevden Ayrılma",
                    Details = $"@{currentUser.Username}, '{task.Title}' görevinden ayrıldı.",
                    CreatedAt = DateTime.Now,
                };

                _dbContext.ActivityLogs.Add(leaveTask);

                await _hubContext.Clients.User(task.TokenId).SendAsync("ReceiveLog", leaveTask.Action, leaveTask.Details);
            }
            else
            {
                var kickFromTask = new ActivityLog
                {
                    TokenId = targetUser.Id.ToString(),
                    Action = "Görevden Çıkarılma",
                    Details = $"@{currentUser.Username}, sizi '{task.Title}' görevinden çıkardı.",
                    CreatedAt = DateTime.Now,
                };

                _dbContext.ActivityLogs.Add(kickFromTask);

                await _hubContext.Clients.User(targetUser.Id.ToString())
                    .SendAsync("ReceiveLog", kickFromTask.Action, kickFromTask.Details);
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