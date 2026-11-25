using CalendarApp.Domain.Validations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace CalendarApp.Web.Validation
{
    public class EmailListAttributeAdapter : AttributeAdapterBase<EmailListAttribute>
    {
        public EmailListAttributeAdapter(EmailListAttribute attribute, IStringLocalizer? stringLocalizer)
            : base(attribute, stringLocalizer)
        {
        }

        public override void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-emaillist", GetErrorMessage(context));
        }

        public override string GetErrorMessage(ModelValidationContextBase validationContext)
        {
            return Attribute.ErrorMessage ?? "One or more of the invited email addresses is invalid.";
        }
    }
}
