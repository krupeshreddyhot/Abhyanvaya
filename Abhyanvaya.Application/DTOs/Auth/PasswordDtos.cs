namespace Abhyanvaya.Application.DTOs.Auth;

public sealed class ForgotPasswordRequest
{
    public required string UniversityCode { get; set; }
    public required string CollegeCode { get; set; }
    public required string Username { get; set; }
}

public sealed class ResetPasswordRequest
{
    public required string ResetToken { get; set; }
    public required string NewPassword { get; set; }
}

public sealed class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}
