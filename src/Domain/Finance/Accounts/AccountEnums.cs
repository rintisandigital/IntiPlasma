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
