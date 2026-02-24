using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskManagerApi.Data;
using TaskManagerApi.Models;
using System.Net;
using System.Net.Mail;

namespace TaskManagerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] User user)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == user.Email)) return BadRequest("Bu Email Kullanımda.");

            if (await _dbContext.Users.AnyAsync(u => u.Username == user.Username))
                return BadRequest("Username already exists.");

            if (user.Password.Length < 6) return BadRequest("Password needs to be more than 6 characters.");

            var existingToken = await _dbContext.EmailTokens.FirstOrDefaultAsync(t => t.Email == user.Email);
            if (existingToken != null) { _dbContext.EmailTokens.Remove(existingToken); }

            Random rnd = new Random();
            string code = rnd.Next(100000, 999999).ToString();

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Password);

            var EmailToken = new EmailToken
            {
                Username = user.Username,
                Password = hashedPassword,
                Email = user.Email,
                Code = code,
                ExpirationDate = DateTime.UtcNow.AddMinutes(3)
            };

            _dbContext.EmailTokens.Add(EmailToken);
            await _dbContext.SaveChangesAsync();

            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(_configuration["Email:Adress"], _configuration["Email:Pass"]),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage()
                {
                    From = new MailAddress(_configuration["Email:Adress"], "Task Manager Doğrulama Kodu"),
                    Subject = "Kayıt Doğrulama Kodu",
                    Body = $"Doğrulama Kodunuz: {code}",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(user.Email);
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch
            {
                return BadRequest("Sistem Hatası Oluştu.");
            }

            return Ok(new { message = "Kayıt Başarılı. Lütfen e-postanıza gelen kodu giriniz.", email = user.Email });
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginUser([FromBody] User user)
        {
            var userFromDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == user.Username);

            if (userFromDb == null || !BCrypt.Net.BCrypt.Verify(user.Password, userFromDb.Password))
                return Unauthorized("Invalid username or password.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var claims = new List<Claim>
            {
                new Claim("UserId", userFromDb.Id.ToString()),
                new Claim("UserName", userFromDb.Username)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(6.30),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                token = tokenString,
                userId = userFromDb.Id,
                username = userFromDb.Username
            });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] EmailToken request)
        {
            var tokenRecord = await _dbContext.EmailTokens.FirstOrDefaultAsync
                (t => t.Email == request.Email && t.Code == request.Code);

            if (tokenRecord == null) return BadRequest("Hatalı Kod");

            if (tokenRecord.ExpirationDate < DateTime.UtcNow)
            {
                _dbContext.EmailTokens.Remove(tokenRecord);
                await _dbContext.SaveChangesAsync();
                return BadRequest("Kodun Süresi Doldu. Lütfen Tekrar Kayıt Olunuz.");
            }

            var newUser = new User
            {
                Username = tokenRecord.Username,
                Password = tokenRecord.Password,
                Email = tokenRecord.Email
            };

            _dbContext.Users.Add(newUser);
            _dbContext.EmailTokens.Remove(tokenRecord);

            var expiredTokens = await _dbContext.EmailTokens.Where(x => x.ExpirationDate < DateTime.UtcNow).ToListAsync();

            if (expiredTokens.Any()) _dbContext.EmailTokens.RemoveRange(expiredTokens);

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Kayıt Başarılı. Giriş Yapabilirsiniz." });
        }
    }
}
