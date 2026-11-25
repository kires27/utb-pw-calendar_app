using CalendarApp.Domain.Validations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace CalendarApp.Web.Validation
{
    public class DateRangeAttributeAdapter : AttributeAdapterBase<DateRangeAttribute>
    {
        public DateRangeAttributeAdapter(DateRangeAttribute attribute, IStringLocalizer? stringLocalizer)
            : base(attribute, stringLocalizer)
        {
        }

        public override void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-daterange", GetErrorMessage(context));
            MergeAttribute(context.Attributes, "data-val-daterange-start", DateRangeAttribute.StartTimePropertyName);
            MergeAttribute(context.Attributes, "data-val-daterange-end", DateRangeAttribute.EndTimePropertyName);
        }

        public override string GetErrorMessage(ModelValidationContextBase validationContext)
        {
            return Attribute.ErrorMessage ?? "Invalid date range.";
        }
    }
}
