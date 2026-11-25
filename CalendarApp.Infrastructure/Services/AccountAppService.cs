using CalendarApp.Domain.Abstraction;
using CalendarApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CalendarApp.Infrastructure.Services
{
    public class AccountAppService : IAccountAppService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public AccountAppService(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<IList<string>> RegisterAsync(string username, string email, string password)
        {
            var user = new User
            {
                UserName = username,
                Email = email,
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                return result.Errors.Select(e => e.Description).ToList();
            }

            await _userManager.AddToRoleAsync(user, "User");

            await _signInManager.SignInAsync(user, isPersistent: false);
            return new List<string>();
        }

        public async Task<bool> LoginAsync(string usernameOrEmail, string password, bool rememberMe)
        {
            var username = usernameOrEmail;

            if (usernameOrEmail.Contains('@'))
            {
                var userByEmail = await _userManager.FindByEmailAsync(usernameOrEmail);
                if (userByEmail == null)
                {
                    return false;
                }

                username = userByEmail.UserName!;
            }

            var result = await _signInManager.PasswordSignInAsync(
                username,
                password,
                rememberMe,
                lockoutOnFailure: false);

            return result.Succeeded;
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }
    }
}
