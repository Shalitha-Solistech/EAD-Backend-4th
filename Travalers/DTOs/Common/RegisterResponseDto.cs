using Travalers.Enums;

namespace Travalers.DTOs.Common
{
    public class RegisterResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public UserType UserType { get; set; }

        public string Token { get; set; }
    }
}
