using CalendarApp.Domain.Validations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace CalendarApp.Web.Validation
{
    public class RequiredIfAttributeAdapter : AttributeAdapterBase<RequiredIfAttribute>
    {
        public RequiredIfAttributeAdapter(RequiredIfAttribute attribute, IStringLocalizer? stringLocalizer)
            : base(attribute, stringLocalizer)
        {
        }

        public override void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-requiredif", GetErrorMessage(context));
            MergeAttribute(context.Attributes, "data-val-requiredif-dependentproperty", Attribute.DependentProperty);
            MergeAttribute(context.Attributes, "data-val-requiredif-targetvalue", Convert.ToString(Attribute.TargetValue)?.ToLowerInvariant() ?? "true");
        }

        public override string GetErrorMessage(ModelValidationContextBase validationContext)
        {
            return Attribute.FormatErrorMessage(validationContext.ModelMetadata.GetDisplayName());
        }
    }
}
