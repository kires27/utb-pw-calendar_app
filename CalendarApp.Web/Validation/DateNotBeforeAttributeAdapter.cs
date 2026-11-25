using CalendarApp.Domain.Validations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace CalendarApp.Web.Validation
{
    public class DateNotBeforeAttributeAdapter : AttributeAdapterBase<DateNotBeforeAttribute>
    {
        public DateNotBeforeAttributeAdapter(DateNotBeforeAttribute attribute, IStringLocalizer? stringLocalizer)
            : base(attribute, stringLocalizer)
        {
        }

        public override void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-datenotbefore", GetErrorMessage(context));
            MergeAttribute(context.Attributes, "data-val-datenotbefore-otherproperty", Attribute.OtherProperty);
        }

        public override string GetErrorMessage(ModelValidationContextBase validationContext)
        {
            return Attribute.FormatErrorMessage(validationContext.ModelMetadata.GetDisplayName());
        }
    }
}
