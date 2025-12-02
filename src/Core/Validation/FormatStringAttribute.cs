using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Ravuno.Core.Validation;

/// <summary>
/// Validates that a string is a valid format string with exactly the specified number of parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class FormatStringAttribute : ValidationAttribute
{
    private readonly int _expectedParameterCount;

    public FormatStringAttribute(int expectedParameterCount = 1)
    {
        this._expectedParameterCount = expectedParameterCount;
        this.ErrorMessage = $"The field must be a valid format string with exactly {this._expectedParameterCount} parameter(s) (e.g., '{{0}}').";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        var formatString = value.ToString()!;

        try
        {
            var testParams = Enumerable.Range(0, this._expectedParameterCount)
                .Select(i => $"test{i}")
                .ToArray();

            _ = string.Format(CultureInfo.InvariantCulture, formatString, testParams.Cast<object>().ToArray());

            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult(
                this.ErrorMessage ?? "Invalid format string.",
                validationContext.MemberName != null ? new[] { validationContext.MemberName } : null);
        }
    }
}