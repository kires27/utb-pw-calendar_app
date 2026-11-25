namespace CalendarApp.Domain.Abstraction
{
    public interface IUserAppService
    {
        Task<IList<UserWithRoleDto>> GetAllUsersAsync();
        Task<UserWithRoleDto?> GetUserByIdAsync(int userId);
        Task<Dictionary<int, UserBasicDto>> GetUsersByIdsAsync(IEnumerable<int> userIds);
        Task<List<UserBasicDto>> FindUsersByEmailsAsync(List<string> emails);
        Task<List<UserBasicDto>> SearchUsersAsync(string term);
        Task<bool> CreateUserAsync(string username, string email, string password, string role);
        Task<UserOperationResultDto> UpdateUserAsync(int userId, string username, string email, string role);
        Task<UserOperationResultDto> SetPasswordAsync(int userId, string newPassword);
        Task<bool> DeleteUserAsync(int userId);
    }

    public class UserOperationResultDto
    {
        public bool Success => Errors.Count == 0;
        public IList<string> Errors { get; } = new List<string>();
    }

    public class UserWithRoleDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
