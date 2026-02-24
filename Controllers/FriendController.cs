using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;
using TaskManagerApi.Data;
using TaskManagerApi.Models;

namespace TaskManagerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FriendController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        public FriendController(AppDbContext dbContext) { _dbContext = dbContext; }
        private string GetUserIdFromHeader()
        {
            var UserIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (!string.IsNullOrEmpty(UserIdClaim)) return UserIdClaim;

            return "unknown";
        }

        [HttpPost("add/{username}")]
        public async Task<IActionResult> AddFriend(string username)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var targetUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (targetUser == null) return BadRequest("Bu kullanıcı adıyla birisi bulunamadı.");

            var targetUserId = targetUser.Id;

            if (targetUserId == currentUserId) return BadRequest("Kendine Arkadaşlık İsteği Gönderemezsin.");

            var existingFriendship = await _dbContext.FriendSystem.FirstOrDefaultAsync(f =>
                (f.RequesterId == currentUserId && f.ReceiverId == targetUserId) ||
                (f.ReceiverId == currentUserId && f.RequesterId == targetUserId));

            if (existingFriendship != null) return BadRequest("Zaten Arkadaşsınız veya Bekleyen Bir İsteğiniz Var.");

            var friendship = new FriendSystem
            {
                RequesterId = currentUserId,
                ReceiverId = targetUserId,
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.FriendSystem.Add(friendship);

            var Current = await _dbContext.Users.FindAsync(currentUserId);

            var log = new ActivityLog
            {
                TokenId = currentUserId.ToString(),
                Action = "Arkadaşlık İsteği",
                Details = $"{Current.Username} Adlı Kullanıcı, {targetUser.Username} " +
                    $"Adlı Kullanıcıya Arkadaşlık İsteği Gönderdi."
            };

            _dbContext.ActivityLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Arkadaşlık İsteği Gönderildi" });
        }

        [HttpDelete("remove/{targetUserId}")]
        public async Task<IActionResult> DeleteFriend(int targetUserId)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());
            var existingFriendship = await _dbContext.FriendSystem.FirstOrDefaultAsync(f => f.RequesterId == currentUserId
                && f.ReceiverId == targetUserId || f.ReceiverId == currentUserId && f.RequesterId == targetUserId);

            if (targetUserId == currentUserId) return BadRequest("Kendini Arkadaşlıktan Çıkaramazsın.");

            if (existingFriendship == null) return BadRequest("Bu Kullanıcı İle Arkadaş Değilsiniz.");

            _dbContext.FriendSystem.Remove(existingFriendship);

            var Current = await _dbContext.Users.FindAsync(currentUserId);
            var Target = await _dbContext.Users.FindAsync(targetUserId);

            var log = new ActivityLog
            {
                TokenId = currentUserId.ToString(),
                Action = "Arkadaşlıktan Çıkarıldınız",
                Details = $"{Current.Username} Adlı Kullanıcı, {Target.Username} Adlı Kullanıcıyı Arkadaşlıktan Çıkardı"
            };

            _dbContext.ActivityLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Arkadaşlıktan Çıkarıldı" });
        }

        [HttpPut("accept/{requesterId}")]
        public async Task<IActionResult> AcceptFriendRequest(int requesterId)
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var friendship = await _dbContext.FriendSystem.FirstOrDefaultAsync(f => f.RequesterId == requesterId &&
                f.ReceiverId == currentUserId);

            if (friendship == null) return BadRequest("Bekleyen Bir İsteğiniz Bulunmadı");

            friendship.IsAccepted = true;

            var Current = await _dbContext.Users.FindAsync(currentUserId);
            var Target = await _dbContext.Users.FindAsync(requesterId);

            var log = new ActivityLog
            {
                TokenId = currentUserId.ToString(),
                Action = "Arkadaş Oldunuz!",
                Details = $"Tebrikler! Artık {Target.Username} ile {Current.Username} arkadaş."
            };

            _dbContext.ActivityLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Artık Arkadaşsınız!" });
        }

        [HttpGet("friendlist")]
        public async Task<IActionResult> GetFriendList()
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var friendList = await _dbContext.FriendSystem
                .Where(x => (x.RequesterId == currentUserId || x.ReceiverId == currentUserId) && x.IsAccepted)
                .Select(f => new
                {
                    FriendId = f.RequesterId == currentUserId ? f.ReceiverId : f.RequesterId,
                    FriendName = f.RequesterId == currentUserId ? f.Receiver.Username : f.Requester.Username
                })
                .ToListAsync();

            return Ok(friendList);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var currentUserId = int.Parse(GetUserIdFromHeader());

            var pendingRequest = await _dbContext.FriendSystem
                .Where(x => x.ReceiverId == currentUserId && x.IsAccepted == false)
                .Select(f => new
                {
                    RequesterId = f.RequesterId,
                    RequesterName = f.Requester.Username
                })
                .ToListAsync();

            return Ok(pendingRequest);
        }
    }
}
