using FluentValidation;
using PubDevMcp.Application.Features.ListVersions;

namespace PubDevMcp.Application.Validators;

public sealed class ListVersionsQueryValidator : AbstractValidator<ListVersionsQuery>
{
    public ListVersionsQueryValidator()
    {
        RuleFor(query => query.Package)
            .Cascade(CascadeMode.Stop)
            .Must(ValidationRules.HasValue)
            .WithMessage("Package name is required.")
            .Must(value => ValidationRules.IsValidPackageName(value!.Trim()))
            .WithMessage("Package name must match pub.dev naming rules (lowercase letters, numbers, and underscores only).");

        RuleFor(query => query.Take)
            .InclusiveBetween(1, 200)
            .WithMessage("Take must be between 1 and 200.");
    }
}
