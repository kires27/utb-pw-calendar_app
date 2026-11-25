using System.ComponentModel.DataAnnotations;

namespace CalendarApp.Domain.Validations
{
    // DateTime property must not be before another DateTime property on the same object
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DateNotBeforeAttribute : ValidationAttribute
    {
        private readonly string _otherProperty;

        public string OtherProperty => _otherProperty;

        public DateNotBeforeAttribute(string otherProperty)
        {
            _otherProperty = otherProperty;
            ErrorMessage ??= "Date cannot be before {0} reference.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var instance = validationContext.ObjectInstance;
            var property = instance.GetType().GetProperty(_otherProperty);
            if (property == null)
            {
                return ValidationResult.Success;
            }

            if (value is DateTime dateValue &&
                property.GetValue(instance) is DateTime otherDate &&
                dateValue < otherDate)
            {
                var displayName = validationContext.DisplayName;
                return new ValidationResult(
                    FormatErrorMessage(displayName),
                    new[] { validationContext.MemberName ?? string.Empty });
            }

            return ValidationResult.Success;
        }
    }
}
