using FluentValidation;

namespace Application.Warehouses.Update;

internal sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Address).MaximumLength(500);
    }
}
