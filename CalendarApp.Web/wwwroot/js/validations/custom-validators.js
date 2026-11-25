// Client-side validation adapters for the custom validation attributes.
// The matching data-val-* attributes are emitted by the adapter classes in
// CalendarApp.Web/Validation, which wrap the attributes in
// CalendarApp.Domain/Validations.

// --- TextLengthRangeAttribute -------------------------------------------------
$.validator.addMethod('textlengthrange', function (value, element, params) {
    if (!value) {
        return true; // Let required validator handle empty values
    }

    var minLength = parseInt(params.min);
    var maxLength = parseInt(params.max);

    return value.length >= minLength && value.length <= maxLength;
});

$.validator.unobtrusive.adapters.add('textlengthrange', ['min', 'max'], function (options) {
    var params = {
        min: options.params.min,
        max: options.params.max
    };
    options.rules['textlengthrange'] = params;
    options.messages['textlengthrange'] = options.message;
});

// --- DateRangeAttribute (start must not be later than end) ---------------------
$.validator.addMethod('daterange', function (value, element, params) {
    var form = $(element).closest('form');
    var startTimeField = form.find('[name="' + params.start + '"]');
    var startTimeValue = startTimeField.val();

    if (!value || !startTimeValue) {
        return true; // Let required validator handle empty values
    }

    var startTime = new Date(startTimeValue);
    var endTime = new Date(value);

    return startTime <= endTime;
});

$.validator.unobtrusive.adapters.add('daterange', ['start', 'end'], function (options) {
    var params = {
        start: options.params.start,
        end: options.params.end
    };
    options.rules['daterange'] = params;
    options.messages['daterange'] = options.message;
});

// --- RequiredIfAttribute (required when dependent property equals target) ------
$.validator.addMethod('requiredif', function (value, element, params) {
    var form = $(element).closest('form');
    var dependentField = form.find('[name="' + params.dependentproperty + '"]');

    if (dependentField.length === 0) {
        return true;
    }

    var dependentValue;
    if (dependentField.is(':checkbox')) {
        dependentValue = dependentField.prop('checked') ? 'true' : 'false';
    } else {
        dependentValue = dependentField.val();
    }

    if (String(dependentValue).toLowerCase() !== String(params.targetvalue).toLowerCase()) {
        return true; // Condition not met, field not required
    }

    // Condition met: field must be filled (self-contained, though the required validator also runs)
    return $.trim(value || '').length > 0;
});

$.validator.unobtrusive.adapters.add('requiredif', ['dependentproperty', 'targetvalue'], function (options) {
    var params = {
        dependentproperty: options.params.dependentproperty,
        targetvalue: options.params.targetvalue
    };
    options.rules['requiredif'] = params;
    options.messages['requiredif'] = options.message;
});

// --- DateNotBeforeAttribute (must not be before another date field) ------------
$.validator.addMethod('datenotbefore', function (value, element, params) {
    var form = $(element).closest('form');
    var otherField = form.find('[name="' + params.otherproperty + '"]');
    var otherValue = otherField.val();

    if (!value || !otherValue) {
        return true; // Let required validator handle empty values
    }

    return new Date(value) >= new Date(otherValue);
});

$.validator.unobtrusive.adapters.add('datenotbefore', ['otherproperty'], function (options) {
    var params = {
        otherproperty: options.params.otherproperty
    };
    options.rules['datenotbefore'] = params;
    options.messages['datenotbefore'] = options.message;
});

// --- EmailListAttribute (comma/semicolon separated valid emails) ---------------
$.validator.addMethod('emaillist', function (value, element, params) {
    if (!value) {
        return true; // Let required validator handle empty values
    }

    var emailRegex = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
    var emails = value.split(/[,;]/).map(function (email) { return $.trim(email); })
        .filter(function (email) { return email.length > 0; });

    return emails.every(function (email) {
        return emailRegex.test(email);
    });
});

$.validator.unobtrusive.adapters.add('emaillist', [], function (options) {
    options.rules['emaillist'] = {};
    options.messages['emaillist'] = options.message;
});
