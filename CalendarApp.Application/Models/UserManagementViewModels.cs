using System.ComponentModel.DataAnnotations;
using CalendarApp.Domain.Abstraction;

namespace CalendarApp.Application.Models
{
    public class UserCreateViewModel
    {
        [Required]
        [RegularExpression("^[a-zA-Z0-9_.-]{3,}$", ErrorMessage = "Username must be at least 3 characters and may only contain letters, digits, '.', '_' and '-'.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Admin|User)$", ErrorMessage = "Role must be Admin or User.")]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;
    }

    public class UserListViewModel
    {
        public IList<UserWithRoleDto> Users { get; set; } = new List<UserWithRoleDto>();
    }

    public class UserEditViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required]
        [RegularExpression("^[a-zA-Z0-9_.-]{3,}$", ErrorMessage = "Username must be at least 3 characters and may only contain letters, digits, '.', '_' and '-'.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Admin|User)$", ErrorMessage = "Role must be Admin or User.")]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmNewPassword { get; set; }

        // Password is optional: blank = keep current password.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var password = NewPassword ?? string.Empty;
            var confirm = ConfirmNewPassword ?? string.Empty;

            if (password.Length == 0 && confirm.Length == 0)
                yield break;

            if (password != confirm)
            {
                yield return new ValidationResult("The new password and confirmation password do not match.", new[] { nameof(ConfirmNewPassword) });
            }

            if (password.Length < 8)
                yield return new ValidationResult("Password must be at least 8 characters long.", new[] { nameof(NewPassword) });
            if (!password.Any(char.IsDigit))
                yield return new ValidationResult("Password must contain at least one digit.", new[] { nameof(NewPassword) });
            if (!password.Any(char.IsUpper))
                yield return new ValidationResult("Password must contain at least one uppercase letter.", new[] { nameof(NewPassword) });
            if (!password.Any(char.IsLower))
                yield return new ValidationResult("Password must contain at least one lowercase letter.", new[] { nameof(NewPassword) });
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                yield return new ValidationResult("Password must contain at least one non-alphanumeric character.", new[] { nameof(NewPassword) });
            if (password.Distinct().Count() < 6)
                yield return new ValidationResult("Password must contain at least 6 unique characters.", new[] { nameof(NewPassword) });
        }
    }
}