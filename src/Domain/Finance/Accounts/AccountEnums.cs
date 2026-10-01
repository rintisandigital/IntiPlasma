namespace Domain.Finance.Accounts;

public enum AccountType
{
    /// <summary>
    /// Aset.
    /// </summary>
    Asset = 1,

    /// <summary>
    /// Liabilitas.
    /// </summary>
    Liability = 2,

    /// <summary>
    /// Ekuitas.
    /// </summary>
    Equity = 3,

    /// <summary>
    /// Pendapatan.
    /// </summary>
    Revenue = 4,

    /// <summary>
    /// Beban (termasuk HPP).
    /// </summary>
    Expense = 5
}

public enum BalanceSide
{
    Debit = 1,
    Credit = 2
}

/// <summary>
/// Section of the cash flow statement (direct method) a cash movement falls in when this account is the counter
/// account of the cash/bank line.
/// </summary>
public enum CashFlowCategory
{
    /// <summary>
    /// Aktivitas operasi (sales, purchases, expenses, taxes).
    /// </summary>
    Operating = 1,

    /// <summary>
    /// Aktivitas investasi (fixed assets).
    /// </summary>
    Investing = 2,

    /// <summary>
    /// Aktivitas pendanaan (capital, long-term loans).
    /// </summary>
    Financing = 3
}
