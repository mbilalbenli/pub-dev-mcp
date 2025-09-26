using FluentValidation;
using PubDevMcp.Application.Features.CheckCompatibility;

namespace PubDevMcp.Application.Validators;

public sealed class CheckCompatibilityQueryValidator : AbstractValidator<CheckCompatibilityQuery>
{
    public CheckCompatibilityQueryValidator()
    {
        RuleFor(query => query.Package)
            .Cascade(CascadeMode.Stop)
            .Must(ValidationRules.HasValue)
            .WithMessage("Package name is required.")
            .Must(value => ValidationRules.IsValidPackageName(value!.Trim()))
            .WithMessage("Package name must match pub.dev naming rules (lowercase letters, numbers, and underscores only).");

        RuleFor(query => query.FlutterSdk)
            .Cascade(CascadeMode.Stop)
            .Must(ValidationRules.HasValue)
            .WithMessage("Flutter SDK version is required.")
            .Must(IsValidFlutterConstraint)
            .WithMessage("Flutter SDK must be a semantic version or valid caret/range constraint.");

        RuleFor(query => query.ProjectConstraint)
            .Must(value => ValidationRules.IsValidVersionRange(value!.Trim()))
            .When(query => ValidationRules.HasValue(query.ProjectConstraint))
            .WithMessage("Project constraint must be a valid semantic version range.");
    }

    private static bool IsValidFlutterConstraint(string? value)
    {
        if (!ValidationRules.HasValue(value))
        {
            return false;
        }

        var trimmed = value!.Trim();
        return ValidationRules.IsValidSemanticVersion(trimmed) || ValidationRules.IsValidVersionRange(trimmed);
    }
}
