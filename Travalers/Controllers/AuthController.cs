using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Travalers.DTOs.Common;
using Travalers.DTOs.User;
using Travalers.Entities;
using Travalers.Repository;
using Travalers.Services;

namespace Travalers.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITicketRepository _ticketRepository;

        public AuthController(IUserRepository userRepository, 
                              IConfiguration configuration,
                              ICurrentUserService currentUserService,
                              ITicketRepository ticketRepository)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _currentUserService = currentUserService;
            _ticketRepository = ticketRepository;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            var response = new RegisterResponseDto();

            if (userDto.Password != userDto.ConfirmPassword)
            {
                response.IsSuccess = false;
                response.Message = "Passwords do not match.";
                return Ok(response);
            }

            // Check if a user with the same NIC exists
            var existingUser = await _userRepository.GetUserByNICAsync(userDto.NIC);

            if (existingUser != null)
            {
                response.IsSuccess = false;
                response.Message = "NIC already exists.";
                return Ok(response);
            }

            string passwordHash = HashPassword(userDto.Password);

            var newUser = new User
            {
                
                Username = userDto.Username,
                PasswordHash = passwordHash,
                UserType = (Enums.UserType)1,
                NIC = userDto.NIC,
                IsActive = true,
                Address = userDto.Address,
                TelNo = userDto.TelNo,
            };

            await _userRepository.CreateUserAsync(newUser);

            response.IsSuccess = true;
            response.Message = "Registration.";
            response.UserType = newUser.UserType;

            return Ok(response);
        }




        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto userDto)
        {

            var response = new RegisterResponseDto();

            var user = await _userRepository.GetUserByNICAsync(userDto.NIC);

            if (user == null)
            {
                response.IsSuccess = false;
                response.Message = "Invalid username or password.";
                return Ok(response);
            }

            if(user.IsActive == false)
            {
                response.IsSuccess = false;
                response.Message = "User Is Deactivated.";
                return Ok(response);
            }
            else
            {
                if (VerifyPassword(userDto.Password, user.PasswordHash))
                {
                    var token = GenerateJwtToken(user.Id, user.NIC);
                    response.IsSuccess = true;
                    response.Message = "Welcome Travaler.";
                    response.Token = token;
                    response.UserType = user.UserType;
                    return Ok(response);
                }
                else
                {
                    response.IsSuccess = false;
                    response.Message = "Invalid username or password.";
                    return Ok(response);
                }
                
            }   
        }

        [HttpPost("updateUser")]
        public async Task<IActionResult> UpdateUser([FromBody] ProfileDto userDto)
        {
            var response = new ResposenDto();

            var userId = _currentUserService.UserId;

            var existingUser = await _userRepository.GetUserByNICAsync(userId);

            existingUser.Username = userDto.Username;
            existingUser.Address = userDto.Address;
            existingUser.TelNo = userDto.TelNo;

            response.IsSuccess = true;

            response.Message = "Profile Update Successful.";

            await _userRepository.UpdateUserAsync(existingUser);

            return Ok(response);
        }

        [HttpGet("GetUserById")]
        public async Task<ActionResult<User>> GetUserById()
        {
            var userId = _currentUserService.UserId;

            var user = await _userRepository.GetUserById(userId);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }

        [HttpGet("GetAllUsers")]
        public async Task<ActionResult<User>> GetAllUsers()
        {
            var user = await _userRepository.GetAll();

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }

        [HttpDelete("deactivateUser{id}")]
        public async Task<ActionResult> DeactivateUser(string id)
        {

            var response = new ResposenDto();

            var user = await _userRepository.GetUserById(id);

            if (user == null)
            {
                response.IsSuccess = false;
                response.Message = "User not Found";
                return Ok(response);
            }

            else
            {
                if(user.IsActive == false)
                {
                    user.IsActive = true;
                }
                else
                {
                    var ticketsCount = (await _ticketRepository.GetTicketByUserId(id)).Count();

                    if (ticketsCount > 0)
                    {
                        response.IsSuccess = false;
                        response.Message = "Cannot Deactivate Due To This User Have Tickets Already";
                        return Ok(response);
                    }
                    else
                    {
                        user.IsActive = false;
                    }
                }
                
                await _userRepository.UpdateUserAsync(user);

                response.IsSuccess = true;
                response.Message = "User Active Status Changed";
                return Ok(response);
            }
        }

        [HttpDelete("DeactivateUserByUser")]
        public async Task<IActionResult> DeactivateUserByUser()
        {
            var response = new ResposenDto();

            var userId = _currentUserService.UserId;

            var existingUser = await _userRepository.GetUserByNICAsync(userId);

            existingUser.IsActive = false;

            response.IsSuccess = true;

            response.Message = "Profile Deactivated Successful.";

            await _userRepository.UpdateUserAsync(existingUser);

            return Ok(response);
        }
        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hashBytes = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            return HashPassword(password) == passwordHash;
        }

        private string GenerateJwtToken(string userId, string nic)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, nic.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, nic),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Issuer"],
                claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
