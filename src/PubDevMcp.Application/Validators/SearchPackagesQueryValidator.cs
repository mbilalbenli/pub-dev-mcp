using FluentValidation;
using PubDevMcp.Application.Features.SearchPackages;

namespace PubDevMcp.Application.Validators;

public sealed class SearchPackagesQueryValidator : AbstractValidator<SearchPackagesQuery>
{
    public SearchPackagesQueryValidator()
    {
        RuleFor(query => query.Query)
            .Cascade(CascadeMode.Stop)
            .Must(ValidationRules.HasValue)
            .WithMessage("Query must not be empty or whitespace.")
            .Must(value => value!.Trim().Length <= ValidationRules.SearchQueryMaxLength)
            .WithMessage($"Query must be at most {ValidationRules.SearchQueryMaxLength} characters.");

        RuleFor(query => query.SdkConstraint)
            .Must(value => ValidationRules.IsValidVersionRange(value!.Trim()))
            .When(query => ValidationRules.HasValue(query.SdkConstraint))
            .WithMessage("SDK constraint must be a valid semantic version range.");
    }
}
