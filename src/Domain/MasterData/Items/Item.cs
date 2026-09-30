using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Items;

/// <summary>
/// Stock item: DOC, pakan (feed), OVK (obat, vaksin, kimia) or live bird. Quantities are stored in the
/// base unit (e.g. KG for feed); alternative units (e.g. SAK = 50 KG) are defined as conversions.
/// </summary>
public sealed class Item : AggregateRoot
{
    private readonly List<ItemUomConversion> _conversions = [];

    private Item(Guid id, string code, string name, ItemCategory category, Guid baseUomId, Guid? taxCodeId)
        : base(id)
    {
        Code = code;
        Name = name;
        Category = category;
        BaseUomId = baseUomId;
        TaxCodeId = taxCodeId;
        IsActive = true;
    }

    private Item()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public ItemCategory Category { get; private set; }

    /// <summary>
    /// Stock, costing and contract prices are expressed per base unit.
    /// </summary>
    public Guid BaseUomId { get; private set; }

    /// <summary>
    /// Default VAT code proposed on purchase and sales lines.
    /// </summary>
    public Guid? TaxCodeId { get; private set; }

    public bool IsActive { get; private set; }
    public IReadOnlyCollection<ItemUomConversion> Conversions => [.. _conversions];

    public static Item Create(string code, string name, ItemCategory category, Guid baseUomId, Guid? taxCodeId) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), category, baseUomId, taxCodeId);

    public void Update(string name, Guid? taxCodeId, bool isActive)
    {
        Name = name.Trim();
        TaxCodeId = taxCodeId;
        IsActive = isActive;
    }

    /// <summary>
    /// Replaces the unit conversions. A factor states how many base units one alternative unit holds.
    /// </summary>
    public Result SetConversions(IEnumerable<(Guid UomId, decimal Factor)> conversions)
    {
        var list = conversions.ToList();

        if (list.Exists(c => c.UomId == BaseUomId))
        {
            return Result.Failure(ItemErrors.ConversionToBaseUom);
        }

        if (list.GroupBy(c => c.UomId).Any(g => g.Count() > 1))
        {
            return Result.Failure(ItemErrors.DuplicateConversion);
        }

        if (list.Exists(c => c.Factor <= 0))
        {
            return Result.Failure(ItemErrors.InvalidConversionFactor);
        }

        _conversions.Clear();
        _conversions.AddRange(list.Select(c => new ItemUomConversion(Id, c.UomId, c.Factor)));

        return Result.Success();
    }

    public Result<decimal> ConvertToBase(Guid uomId, decimal quantity)
    {
        if (uomId == BaseUomId)
        {
            return quantity;
        }

        ItemUomConversion? conversion = _conversions.Find(c => c.UomId == uomId);

        return conversion is null
            ? Result.Failure<decimal>(ItemErrors.UnknownUom(Id, uomId))
            : quantity * conversion.Factor;
    }
}
