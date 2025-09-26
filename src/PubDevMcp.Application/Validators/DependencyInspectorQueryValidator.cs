using FluentValidation;
using PubDevMcp.Application.Features.DependencyInspector;

namespace PubDevMcp.Application.Validators;

public sealed class DependencyInspectorQueryValidator : AbstractValidator<DependencyInspectorQuery>
{
    public DependencyInspectorQueryValidator()
    {
        RuleFor(query => query.Package)
            .Cascade(CascadeMode.Stop)
            .Must(ValidationRules.HasValue)
            .WithMessage("Package name is required.")
            .Must(value => ValidationRules.IsValidPackageName(value!.Trim()))
            .WithMessage("Package name must match pub.dev naming rules (lowercase letters, numbers, and underscores only).");

        RuleFor(query => query.Version)
            .Must(value => ValidationRules.IsValidSemanticVersion(value!.Trim()))
            .When(query => ValidationRules.HasValue(query.Version))
            .WithMessage("Version must be a valid semantic version when provided.");
    }
}
