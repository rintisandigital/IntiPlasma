using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel;

namespace Infrastructure.Database;

/// <summary>
/// Column mappings for value objects that are shared by several entities.
/// </summary>
internal static class ConfigurationExtensions
{
    public const int MoneyPrecision = 18;

    public static void ComplexMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        System.Linq.Expressions.Expression<Func<TEntity, Money?>> property,
        string columnName)
        where TEntity : class =>
        builder.ComplexProperty(property, money => money
            .Property(m => m.Amount)
            .HasColumnName(columnName)
            .HasPrecision(MoneyPrecision, Money.Decimals));

    public static void ComplexTaxIdentity<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        System.Linq.Expressions.Expression<Func<TEntity, TaxIdentity?>> property)
        where TEntity : class =>
        builder.ComplexProperty(property, tax =>
        {
            tax.Property(t => t.Npwp).HasColumnName("tax_identity_npwp").HasMaxLength(16);
            tax.Property(t => t.Nitku).HasColumnName("tax_identity_nitku").HasMaxLength(22);
            tax.Property(t => t.IsPkp).HasColumnName("tax_identity_is_pkp");
        });

    public static void ComplexBankAccount<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        System.Linq.Expressions.Expression<Func<TEntity, BankAccount?>> property)
        where TEntity : class =>
        builder.ComplexProperty(property, bank =>
        {
            bank.Property(b => b.BankName).HasColumnName("bank_account_bank_name").HasMaxLength(100);
            bank.Property(b => b.AccountNumber).HasColumnName("bank_account_account_number").HasMaxLength(40);
            bank.Property(b => b.AccountHolderName).HasColumnName("bank_account_account_holder_name").HasMaxLength(150);
        });
}
