using CalendarApp.Application.Abstraction;
using CalendarApp.Application.Models;
using CalendarApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CalendarApp.Infrastructure.Services
{
    public class UserAppService : IUserAppService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;

        public UserAppService(UserManager<User> userManager, RoleManager<Role> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IList<UserWithRoleDto>> GetAllUsersAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var userDtos = new List<UserWithRoleDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserWithRoleDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? "User",
                    CreatedAt = user.CreatedAt
                });
            }

            return userDtos;
        }

        public async Task<UserWithRoleDto?> GetUserByIdAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return null;

            var roles = await _userManager.GetRolesAsync(user);
            return new UserWithRoleDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "User",
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<Dictionary<int, UserBasicDto>> GetUsersByIdsAsync(IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().ToList();
            var users = await _userManager.Users
                .Where(u => ids.Contains(u.Id))
                .ToListAsync();

            return users.ToDictionary(
                u => u.Id,
                u => new UserBasicDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty
                });
        }

        public Task<List<UserBasicDto>> FindUsersByEmailsAsync(List<string> emails)
        {
            return _userManager.Users
                .Where(u => u.Email != null && emails.Contains(u.Email))
                .Select(u => new UserBasicDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty
                })
                .ToListAsync();
        }

        public Task<List<UserBasicDto>> SearchUsersAsync(string term)
        {
            return _userManager.Users
                .Where(u => (u.UserName != null && u.UserName.Contains(term)) ||
                            (u.Email != null && u.Email.Contains(term)))
                .OrderBy(u => u.UserName)
                .Take(10)
                .Select(u => new UserBasicDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty
                })
                .ToListAsync();
        }

        public async Task<bool> CreateUserAsync(string username, string email, string password, string role)
        {
            var user = new User
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return false;

            if (!await _roleManager.RoleExistsAsync(role))
                return false;

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            return roleResult.Succeeded;
        }

        public async Task<UserOperationResultDto> UpdateUserAsync(int userId, string username, string email, string role)
        {
            var result = new UserOperationResultDto();
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                result.Errors.Add("User not found.");
                return result;
            }

            // Pre-check uniqueness so the user gets a clear message instead of a store exception.
            if (!string.Equals(user.UserName, username, StringComparison.Ordinal))
            {
                var existingByName = await _userManager.FindByNameAsync(username);
                if (existingByName != null && existingByName.Id != user.Id)
                    result.Errors.Add($"Username '{username}' is already taken.");
            }

            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var existingByEmail = await _userManager.FindByEmailAsync(email);
                if (existingByEmail != null && existingByEmail.Id != user.Id)
                    result.Errors.Add($"Email '{email}' is already taken.");
            }

            if (!await _roleManager.RoleExistsAsync(role))
                result.Errors.Add($"Role '{role}' does not exist.");

            if (!result.Success)
                return result;

            if (!string.Equals(user.UserName, username, StringComparison.Ordinal))
                AddErrors(result, await _userManager.SetUserNameAsync(user, username));

            if (result.Success && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                AddErrors(result, await _userManager.SetEmailAsync(user, email));

            if (result.Success)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    if (currentRoles.Count > 0)
                        AddErrors(result, await _userManager.RemoveFromRolesAsync(user, currentRoles));

                    if (result.Success)
                        AddErrors(result, await _userManager.AddToRoleAsync(user, role));
                }
            }

            if (result.Success)
            {
                // Invalidate the edited user's cookie so role claims refresh on next login.
                await _userManager.UpdateSecurityStampAsync(user);

                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            return result;
        }

        public async Task<UserOperationResultDto> SetPasswordAsync(int userId, string newPassword)
        {
            var result = new UserOperationResultDto();
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                result.Errors.Add("User not found.");
                return result;
            }

            // Reset flow keeps password hashing inside Identity; the admin never sees the hash.
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            AddErrors(result, await _userManager.ResetPasswordAsync(user, token, newPassword));

            if (result.Success)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            return result;
        }

        private static void AddErrors(UserOperationResultDto result, IdentityResult identityResult)
        {
            foreach (var error in identityResult.Errors)
            {
                result.Errors.Add(error.Description);
            }
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded;
        }
    }
}
