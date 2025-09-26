using FluentValidation;
using PubDevMcp.Application.Features.PublisherPackages;

namespace PubDevMcp.Application.Validators;

public sealed class PublisherPackagesQueryValidator : AbstractValidator<PublisherPackagesQuery>
{
    public PublisherPackagesQueryValidator()
    {
        RuleFor(query => query.Publisher)
            .Cascade(CascadeMode.Stop)
            .Must(ValidationRules.HasValue)
            .WithMessage("Publisher identifier is required.")
            .Must(value => ValidationRules.IsValidPublisherId(value!.Trim()))
            .WithMessage("Publisher identifier must contain only lowercase letters, numbers, periods, underscores, or hyphens.");
    }
}
