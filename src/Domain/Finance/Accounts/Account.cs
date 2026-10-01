using Domain.Common;
using SharedKernel;

namespace Domain.Finance.Accounts;

/// <summary>
/// Chart of accounts (COA) entry. Header accounts group other accounts and cannot receive postings;
/// only postable (detail) accounts appear on journal lines. The code, type, parent and postability are
/// fixed at creation because historical postings and reports depend on them.
/// </summary>
public sealed class Account : AggregateRoot
{
    private Account(
        Guid id,
        string code,
        string name,
        AccountType type,
        BalanceSide normalBalance,
        Guid? parentId,
        bool isPostable)
        : base(id)
    {
        Code = code;
        Name = name;
        Type = type;
        NormalBalance = normalBalance;
        ParentId = parentId;
        IsPostable = isPostable;
        IsActive = true;
    }

    private Account()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public AccountType Type { get; private set; }

    /// <summary>
    /// Debit for assets and expenses, credit for liabilities, equity and revenue. Contra accounts
    /// (e.g. akumulasi penyusutan, retur penjualan) override the default.
    /// </summary>
    public BalanceSide NormalBalance { get; private set; }

    public Guid? ParentId { get; private set; }
    public bool IsPostable { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<Account> Create(
        string code,
        string name,
        AccountType type,
        Account? parent,
        bool isPostable,
        BalanceSide? normalBalance = null)
    {
        if (parent is not null)
        {
            if (parent.IsPostable)
            {
                return Result.Failure<Account>(AccountErrors.ParentMustBeHeader(parent.Id));
            }

            if (parent.Type != type)
            {
                return Result.Failure<Account>(AccountErrors.ParentTypeMismatch);
            }
        }

        return new Account(
            Guid.CreateVersion7(),
            Codes.Normalize(code),
            name.Trim(),
            type,
            normalBalance ?? DefaultNormalBalance(type),
            parent?.Id,
            isPostable)
        {
            CashFlowCategory = DefaultCashFlowCategory(type)
        };
    }

    /// <summary>
    /// Cash flow section when this account is the counter account of a cash movement. Defaults to financing for
    /// equity and operating otherwise; fixed assets (investing) and long-term loans (financing) are set explicitly.
    /// </summary>
    public CashFlowCategory CashFlowCategory { get; private set; } = CashFlowCategory.Operating;

    public static BalanceSide DefaultNormalBalance(AccountType type) =>
        type is AccountType.Asset or AccountType.Expense ? BalanceSide.Debit : BalanceSide.Credit;

    public static CashFlowCategory DefaultCashFlowCategory(AccountType type) =>
        type == AccountType.Equity ? CashFlowCategory.Financing : CashFlowCategory.Operating;

    public void Update(string name, bool isActive)
    {
        Name = name.Trim();
        IsActive = isActive;
    }

    public void SetCashFlowCategory(CashFlowCategory category)
    {
        CashFlowCategory = category;
    }
}
