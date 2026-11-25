using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CalendarApp.Domain.Validations
{
    // Validates email addresses separated by commas or semicolons
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class EmailListAttribute : ValidationAttribute
    {
        private static readonly Regex EmailRegex =
            new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled);

        public EmailListAttribute()
        {
            ErrorMessage = "One or more of the invited email addresses is invalid.";
        }

        public static string[] SplitEmails(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string text || string.IsNullOrWhiteSpace(text))
            {
                return ValidationResult.Success;
            }

            var emails = SplitEmails(text);
            foreach (var email in emails)
            {
                if (!EmailRegex.IsMatch(email))
                {
                    var displayName = validationContext.DisplayName;
                    return new ValidationResult(
                        FormatErrorMessage(displayName),
                        new[] { validationContext.MemberName ?? string.Empty });
                }
            }

            return ValidationResult.Success;
        }
    }
}
