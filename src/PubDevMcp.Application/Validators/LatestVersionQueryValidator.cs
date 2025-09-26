using FluentValidation;
using PubDevMcp.Application.Features.LatestVersion;

namespace PubDevMcp.Application.Validators;

public sealed class LatestVersionQueryValidator : AbstractValidator<LatestVersionQuery>
{
    public LatestVersionQueryValidator()
    {
        RuleFor(query => query.Package)
            .Cascade(CascadeMode.Stop)
            .Must(ValidationRules.HasValue)
            .WithMessage("Package name is required.")
            .Must(value => ValidationRules.IsValidPackageName(value!.Trim()))
            .WithMessage("Package name must match pub.dev naming rules (lowercase letters, numbers, and underscores only).");
    }
}
