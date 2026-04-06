using System.Text.RegularExpressions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagerApi.Data;
using TaskManagerApi.Dto;
using TaskManagerApi.Models;
using Group = TaskManagerApi.Models.Group;

namespace TaskManagerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly AppDbContext _dbcontext;

        public GroupController(AppDbContext dbcontext) { _dbcontext = dbcontext; }

        private string GetUserIdFromHeader()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (!string.IsNullOrEmpty(userIdClaim)) return userIdClaim;
            return "unknown";
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto request)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            if (request.Description == null) request.Description = "unknown";

            var newGroup = new Group
            {
                Name = request.Name,
                Description = request.Description,
                CreatorId = currentUserId,
                CreatedAt = DateTime.UtcNow,
            };

            _dbcontext.Groups.Add(newGroup);
            await _dbcontext.SaveChangesAsync();

            var adminMember = new GroupMembers
            {
                GroupId = newGroup.Id,
                UserId = currentUserId,
                isAdmin = true,
                JoinedAt = DateTime.UtcNow,
            };

            _dbcontext.GroupMembers.Add(adminMember);

            _dbcontext.ActivityLogs.Add(new ActivityLog
            {
                TokenId = currentUserId.ToString(),
                Action = "Grup Oluşturuldu",
                Details = $"{newGroup.Name} adında yeni bir grup oluşturdunuz.",
                CreatedAt = DateTime.Now,
            });

            await _dbcontext.SaveChangesAsync();

            return Ok(new { message = "Grup Başarıyla Oluşturuldu", groupId = newGroup.Id });
        }

        [HttpGet("my-groups")]
        public async Task<IActionResult> GetMyGroups()
        {
            var currentUser = int.Parse(GetUserIdFromHeader());

            var groups = await _dbcontext.GroupMembers
                .Where(x => x.UserId == currentUser)
                .Select(x => new
                {
                    GroupId = x.group.Id,
                    Name = x.group.Name,
                    Description = x.group.Description,
                    isAdmin = x.isAdmin,
                    MemberCount = x.group.Members.Count()
                }).ToListAsync();

            return Ok(groups);
        }

        [HttpGet("{groupId}/members")]
        public async Task<IActionResult> GetGroupMembers(int groupId)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var isMember = await _dbcontext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);

            if (!isMember)
                return StatusCode(StatusCodes.Status403Forbidden, "Bu grubun üyelerini görme yetkiniz yok.");

            var members = await _dbcontext.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Select(gm => new
                {
                    UserId = gm.user.Id,
                    Username = gm.user.Username,
                    isAdmin = gm.isAdmin,
                    JoinedAt = gm.JoinedAt
                })
                .ToListAsync();

            return Ok(members);
        }

        [HttpPost("{groupId}/add-member/{username}")]
        public async Task<IActionResult> AddMemberToGroup(int groupId, string username)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var group = await _dbcontext.Groups.FindAsync(groupId);
            if (group == null) return BadRequest("Grup Bulunamadı.");

            var currentMemberStatus = await _dbcontext.GroupMembers.FirstOrDefaultAsync
                (x => x.UserId == currentUserId && x.GroupId == groupId);

            if (currentMemberStatus == null || !currentMemberStatus.isAdmin)
                return StatusCode(StatusCodes.Status403Forbidden, "Bu işlem için yetkiniz yok");

            var targetUser = await _dbcontext.Users.FirstOrDefaultAsync(x => x.Username == username);
            if (targetUser == null) return BadRequest("Böyle bir kullanıcı yok.");

            var friendCheck = await _dbcontext.FriendSystem.FirstOrDefaultAsync
                (x => (x.Receiver.Id == targetUser.Id && x.Requester.Id == currentUserId) ||
                (x.ReceiverId == currentUserId && x.Requester.Id == targetUser.Id));

            if (friendCheck == null) return BadRequest("Bu kullanıcı arkadaşınız değil.");

            var isAlreadyMember = await _dbcontext.GroupMembers.AnyAsync
                (x => x.GroupId == groupId && x.UserId == targetUser.Id);

            if (isAlreadyMember) return BadRequest("Kullanıcı zaten grupta.");

            var newMember = new GroupMembers
            {
                GroupId = groupId,
                UserId = targetUser.Id,
                isAdmin = false,
                JoinedAt = DateTime.UtcNow,
            };

            _dbcontext.GroupMembers.Add(newMember);

            var currentUser = await _dbcontext.Users.FirstOrDefaultAsync(x => x.Id == currentUserId);

            _dbcontext.ActivityLogs.Add(new ActivityLog
            {
                TokenId = targetUser.Id.ToString(),
                Action = "Gruba Eklendiniz",
                Details = $"{currentUser.Username} adlı kişi sizi {group.Name} adlı gruba ekledi.",
                CreatedAt = DateTime.Now,
            });

            await _dbcontext.SaveChangesAsync();
            return Ok(new { message = "Kullanıcı gruba başarıyla eklendi." });
        }

        [HttpDelete("{groupId}/leave")]
        public async Task<IActionResult> LeaveGroup(int groupId)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var member = await _dbcontext.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);

            if (member == null) return NotFound("Bu gruba zaten üye değilsiniz.");

            if (member.isAdmin)
            {
                var otherAdmins = await _dbcontext.GroupMembers
                    .AnyAsync(gm => gm.GroupId == groupId && gm.UserId != currentUserId && gm.isAdmin);

                if (!otherAdmins) return 
                        BadRequest("Gruptaki tek yöneticisiniz. Ayrılmadan önce başkasını yönetici yapın veya grubu silin.");
            }

            _dbcontext.GroupMembers.Remove(member);

            var currentUser = await _dbcontext.Users.FindAsync(currentUserId);
            var group = await _dbcontext.Groups.FindAsync(groupId);

            _dbcontext.ActivityLogs.Add(new ActivityLog
            {
                TokenId = currentUserId.ToString(),
                Action = "Gruptan Ayrıldınız",
                Details = $"'{group.Name}' grubundan başarıyla ayrıldınız.",
                CreatedAt = DateTime.Now,
            });

            await _dbcontext.SaveChangesAsync();

            return Ok(new { message = "Gruptan başarıyla ayrıldınız." });
        }

        [HttpPost("{groupId}/remove-member/{userId}")]
        public async Task<IActionResult> RemoveMember(int groupId, int userId)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var group = await _dbcontext.Groups.FindAsync(groupId);
            if (group == null) return BadRequest("Grup Bulunamadı.");

            var currentMemberStatus = await _dbcontext.GroupMembers.FirstOrDefaultAsync
                (x => x.UserId == currentUserId && x.GroupId == groupId);

            if (currentMemberStatus == null || !currentMemberStatus.isAdmin)
                return StatusCode(StatusCodes.Status403Forbidden, "Bu işlem için yetkiniz yok");

            if (currentUserId == userId) return BadRequest("Kendinizi Gruptan Çıkaramazsınız. Ayrılma Seçeneğini Seçiniz.");

            var memberToRemove = await _dbcontext.GroupMembers.FirstOrDefaultAsync
                (x => x.UserId == userId && x.GroupId == groupId);

            if (memberToRemove == null) return BadRequest("Böyle bir kullanıcı yok.");

            _dbcontext.GroupMembers.Remove(memberToRemove);

            var targetUser = await _dbcontext.Users.FindAsync(userId);
            var currentUser = await _dbcontext.Users.FindAsync(currentUserId);

            _dbcontext.ActivityLogs.Add(new ActivityLog
            {
                TokenId = targetUser.Id.ToString(),
                Action = "Gruptan Çıkarıldınız",
                Details = $"@{currentUser.Username} sizi '{group.Name}' grubundan çıkardı.",
                CreatedAt = DateTime.Now,
            });

            await _dbcontext.SaveChangesAsync();
            return Ok(new { message = "Kullanıcı gruptan başarıyla çıkarıldı." });
        }

        [HttpDelete("{groupId}/delete")]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var group = await _dbcontext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound("Grup bulunamadı.");

            var currentMemberStatus = await _dbcontext.GroupMembers.FirstOrDefaultAsync
                (x => x.UserId == currentUserId && x.GroupId == groupId);

            if (currentMemberStatus == null || !currentMemberStatus.isAdmin)
                return StatusCode(StatusCodes.Status403Forbidden, "Sadece grup yöneticileri grubu silebilir.");

            var members = _dbcontext.GroupMembers.Where(gm => gm.GroupId == groupId);
            _dbcontext.GroupMembers.RemoveRange(members);

            var tasks = _dbcontext.TaskItems.Where(t => t.GroupId == groupId);
            _dbcontext.TaskItems.RemoveRange(tasks);

            _dbcontext.Groups.Remove(group);
            await _dbcontext.SaveChangesAsync();

            return Ok(new { message = "Gruba ait tüm veriler başarıyla silindi." });
        }

        [HttpPut("{groupId}/toggle-admin/{userId}")]
        public async Task<IActionResult> ToggleAdminStatus(int groupId, int userId)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var currentMemberStatus = await _dbcontext.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);

            if (currentMemberStatus == null || !currentMemberStatus.isAdmin)
                return StatusCode(StatusCodes.Status403Forbidden, "Bu işlem için yönetici yetkiniz yok.");

            var targetMember = await _dbcontext.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (targetMember == null) return NotFound("Kullanıcı bu grupta bulunamadı.");

            if (targetMember.isAdmin && currentUserId == userId)
            {
                var adminCount = await _dbcontext.GroupMembers
                    .CountAsync(gm => gm.GroupId == groupId && gm.isAdmin);

                if (adminCount <= 1)
                    return BadRequest("Gruptaki tek yöneticisiniz. Kendi yetkinizi alamazsınız.");
            }

            targetMember.isAdmin = !targetMember.isAdmin;

            if (currentUserId != userId)
            {
                var targetUser = await _dbcontext.Users.FindAsync(userId);
                var currentUser = await _dbcontext.Users.FindAsync(currentUserId);
                var group = await _dbcontext.Groups.FindAsync(groupId);

                string actionText = targetMember.isAdmin ? "Yönetici Yapıldınız" : "Yöneticilikten Alındınız";
                string detailsText = targetMember.isAdmin
                    ? $"@{currentUser.Username} sizi '{group.Name}' grubunda yönetici yaptı."
                    : $"@{currentUser.Username} sizin '{group.Name}' grubundaki yöneticiliğinizi aldı.";

                _dbcontext.ActivityLogs.Add(new ActivityLog
                {
                    TokenId = targetUser.Id.ToString(),
                    Action = actionText,
                    Details = detailsText,
                    CreatedAt = DateTime.Now,
                });
            }

            await _dbcontext.SaveChangesAsync();

            return Ok(new
            {
                message = "Kullanıcının yönetici durumu güncellendi.",
                isAdmin = targetMember.isAdmin,
            });
        }
    }
}
