using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Branches;

/// <summary>
/// Operational branch (cabang) of the company. Every transaction and journal line belongs to a branch.
/// The code is immutable because it is embedded in document numbers (e.g. PO/BR01/2026/IX/0001).
/// </summary>
public sealed class Branch : AggregateRoot
{
    private Branch(Guid id, string code, string name, string? address, string? phone)
        : base(id)
    {
        Code = code;
        Name = name;
        Address = address;
        Phone = phone;
        IsActive = true;
    }

    private Branch()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }

    public static Branch Create(string code, string name, string? address, string? phone) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), address, phone);

    public void Update(string name, string? address, string? phone, bool isActive)
    {
        Name = name.Trim();
        Address = address;
        Phone = phone;
        IsActive = isActive;
    }
}
