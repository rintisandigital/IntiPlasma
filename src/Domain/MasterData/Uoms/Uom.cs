using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Uoms;

/// <summary>
/// Unit of measure (satuan), e.g. EKOR, KG, SAK, BTL.
/// </summary>
public sealed class Uom : AggregateRoot
{
    private Uom(Guid id, string code, string name)
        : base(id)
    {
        Code = code;
        Name = name;
    }

    private Uom()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }

    public static Uom Create(string code, string name) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim());

    public void Update(string name)
    {
        Name = name.Trim();
    }
}
