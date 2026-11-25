using CalendarApp.Domain.Validations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace CalendarApp.Web.Validation
{
    public class TextLengthRangeAttributeAdapter : AttributeAdapterBase<TextLengthRangeAttribute>
    {
        public TextLengthRangeAttributeAdapter(TextLengthRangeAttribute attribute, IStringLocalizer? stringLocalizer)
            : base(attribute, stringLocalizer)
        {
        }

        public override void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-textlengthrange", GetErrorMessage(context));
            MergeAttribute(context.Attributes, "data-val-textlengthrange-min", Attribute.MinLength.ToString());
            MergeAttribute(context.Attributes, "data-val-textlengthrange-max", Attribute.MaxLength.ToString());
        }

        public override string GetErrorMessage(ModelValidationContextBase validationContext)
        {
            return Attribute.ErrorMessage ?? "Invalid text length.";
        }
    }
}
