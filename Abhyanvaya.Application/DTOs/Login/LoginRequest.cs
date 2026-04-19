namespace Abhyanvaya.Application.DTOs.Login
{
    public class LoginRequest
    {
        public string UniversityCode { get; set; } = string.Empty;
        public string CollegeCode { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
