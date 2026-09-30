using Domain.Common;
using SharedKernel;

namespace Domain.Finance.CostCenters;

/// <summary>
/// Pusat biaya, an optional analysis dimension on journal lines (e.g. per unit or department).
/// Branches are a separate dimension carried by every journal.
/// </summary>
public sealed class CostCenter : AggregateRoot
{
    private CostCenter(Guid id, string code, string name)
        : base(id)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    private CostCenter()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    public static CostCenter Create(string code, string name) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim());

    public void Update(string name, bool isActive)
    {
        Name = name.Trim();
        IsActive = isActive;
    }
}
