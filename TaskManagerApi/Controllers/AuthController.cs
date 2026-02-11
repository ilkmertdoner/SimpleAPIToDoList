using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
    public class AuthController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;
        // Add app.settings file to the project
        // Create JWT "Key, Issuer, Audience" in app.settings file

        public AuthController(AppDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public IActionResult RegisterUser([FromBody] User user)
        {
            if (_dbContext.Users.Any(u => u.Username == user.Username)) return BadRequest("Username already exists.");

            _dbContext.Users.Add(user);
            _dbContext.SaveChanges();

            return Ok(new { message = "Successfully Registered!" });
        }

        [HttpPost("login")]
        public IActionResult LoginUser([FromBody] User user)
        {
            var userControl = _dbContext.Users.FirstOrDefault(u => u.Username == user.Username && u.Password == user.Password);

            if (userControl == null) return Unauthorized("Invalid username or password.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            
            var claims = new List<Claim>
            {
                new Claim("UserId", user.Id.ToString()),
                new Claim("UserName", user.Username)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { message = tokenString, UserId = user.Id });
        }
    }
}
