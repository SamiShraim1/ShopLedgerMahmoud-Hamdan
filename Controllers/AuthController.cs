using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShopLedger.Data.DTOs.Auth;
using ShopLedger.Data.Models;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ShopLedger.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserResponseDTO>> LoginAsync([FromBody] LoginRequestDTO loginRequest)
        {
            if (string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password))
                return BadRequest("Invalid email or password");

            var user = await _userManager.FindByEmailAsync(loginRequest.Email);
            if (user == null) return Unauthorized("Invalid email or password");

            //  منع تسجيل الدخول لو Soft Deleted
            //if (user.IsDeleted)
            //    return Unauthorized("تم تعطيل هذا الحساب.");

            var isPassValid = await _userManager.CheckPasswordAsync(user, loginRequest.Password);
            if (!isPassValid) return Unauthorized("Invalid email or password");


            return Ok(new UserResponseDTO { Token = await CreateTokenAsync(user) });
        }

        [HttpPost("Register")]
        [Authorize(Roles=("Admin"))]
        public async Task<ActionResult<UserResponseDTO>> RegisterAsync(RegisterRequestDTO RegisterRequest)
        {
            var user = new ApplicationUser()
            {
                Email = RegisterRequest.Email,
                UserName = RegisterRequest.UserName,
            };

            var result = await _userManager.CreateAsync(user, RegisterRequest.Password);

            if (result.Succeeded)
            {
                return new UserResponseDTO()
                {
                    Token = await CreateTokenAsync(user)
                };
            }
            else
            {
                return BadRequest(new
                {
                    Errors = result.Errors.Select(e => e.Description)
                });
            }
        }

        private async Task<string> CreateTokenAsync(ApplicationUser user)
        {
            var Claims = new List<Claim>()
             {
        new Claim("Email", user.Email),
        new Claim("Name", user.UserName),
        new Claim("Id", user.Id.ToString())
             };

            var Roles = await _userManager.GetRolesAsync(user);
            foreach (var role in Roles)
            {
                Claims.Add(new Claim("role", role));
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("jwtOptions")["SecretKey"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: Claims,
                expires: DateTime.Now.AddDays(15),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
