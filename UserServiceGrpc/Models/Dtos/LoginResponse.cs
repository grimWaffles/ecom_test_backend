namespace UserServiceGrpc.Models.Dtos
{
    public class LoginResponseDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public bool IsSuccess => UserId > 0 && string.IsNullOrEmpty(ErrorMessage);
    }
}
