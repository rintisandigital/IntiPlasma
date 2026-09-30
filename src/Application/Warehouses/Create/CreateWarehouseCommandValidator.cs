using FluentValidation;

namespace Application.Warehouses.Create;

internal sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.Address).MaximumLength(500);
    }
}
