using System.ComponentModel.DataAnnotations;
using CalendarApp.Domain.Validations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace CalendarApp.Web.Validation
{
    public class CustomValidationAttributeAdapterProvider : IValidationAttributeAdapterProvider
    {
        private readonly IValidationAttributeAdapterProvider _defaultProvider = new ValidationAttributeAdapterProvider();

        public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
        {
            return attribute switch
            {
                DateRangeAttribute dateRange => new DateRangeAttributeAdapter(dateRange, stringLocalizer),
                TextLengthRangeAttribute textLengthRange => new TextLengthRangeAttributeAdapter(textLengthRange, stringLocalizer),
                RequiredIfAttribute requiredIf => new RequiredIfAttributeAdapter(requiredIf, stringLocalizer),
                EmailListAttribute emailList => new EmailListAttributeAdapter(emailList, stringLocalizer),
                DateNotBeforeAttribute dateNotBefore => new DateNotBeforeAttributeAdapter(dateNotBefore, stringLocalizer),
                _ => _defaultProvider.GetAttributeAdapter(attribute, stringLocalizer)
            };
        }
    }
}
