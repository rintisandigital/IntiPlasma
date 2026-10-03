using System.Globalization;
using Application.Finance.CashBank;
using Application.Finance.Payables;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Finance.Documents;

/// <summary>
/// Printed finance documents (PLAN-WEBAPP §5.3): payment voucher and cash-in/cash-out voucher (BKM/BKK). English,
/// with the amount in words in Indonesian (W-17).
/// </summary>
public static class FinanceDocumentPdf
{
    private static readonly string[] AllocationHeaders = ["Document", "Reference", "Date", "Amount"];
    private static readonly string[] CashLineHeaders = ["#", "Account", "Cost center", "Description", "Amount"];

    public static void PaymentVoucher(IContainer container, PaymentVoucherResponse voucher, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        ArgumentNullException.ThrowIfNull(fmt);

        bool plasma = voucher.PayeeType == "Farmer";
        DocumentPdf.Compose(container, column =>
        {
            column.Item().Element(c => DocumentPdf.Fields(c,
            [
                ("Pay to", $"{voucher.PayeeName} ({voucher.PayeeCode}){(plasma ? " — plasma" : string.Empty)}"),
                ("Voucher", voucher.Number),
                ("Payment date", fmt.Date(voucher.PaymentDate)),
                ("Pay from", $"{voucher.CashBankCode} — {voucher.CashBankName}"),
                ("Reference", voucher.Reference ?? "—"),
                ("Status", voucher.Status == "Cancelled" ? $"Cancelled — {voucher.CancellationReason}" : EnumOptions.Label(voucher.Status))
            ]));

            column.Item().Text(plasma ? "Payment of plasma settlements" : "Payment of vendor invoices").Bold().FontSize(10);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.4f);
                });
                DocumentPdf.Header(table, AllocationHeaders);
                foreach (PaymentAllocationResponse allocation in voucher.Allocations ?? [])
                {
                    table.Cell().Element(PdfLayout.Cell).Text(allocation.Number ?? "—");
                    table.Cell().Element(PdfLayout.Cell).Text(allocation.Reference);
                    table.Cell().Element(PdfLayout.Cell).Text(fmt.Date(allocation.DocumentDate));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(allocation.Amount));
                }
            });

            DocumentPdf.Totals(column, [("Total paid", voucher.Amount, true)]);
            DocumentPdf.InWords(column, voucher.Amount);
            DocumentPdf.Notes(column, voucher.Notes);
            DocumentPdf.Signatures(column, ["Prepared by", "Approved by", "Paid by", "Received by"]);
        });
    }

    public static void CashTransaction(IContainer container, CashTransactionResponse transaction, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(fmt);

        bool cashIn = transaction.Direction == "In";
        DocumentPdf.Compose(container, column =>
        {
            column.Item().Element(c => DocumentPdf.Fields(c,
            [
                (cashIn ? "Received into" : "Paid from", $"{transaction.CashBankCode} — {transaction.CashBankName}"),
                ("Voucher", transaction.Number ?? "DRAFT"),
                ("Date", fmt.Date(transaction.Date)),
                ("Reference", transaction.Reference ?? "—"),
                ("Description", transaction.Description),
                ("Status", transaction.Status == "Cancelled" ? $"Cancelled — {transaction.CancellationReason}" : EnumOptions.Label(transaction.Status))
            ]));

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);
                    c.RelativeColumn(2.4f);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.4f);
                });
                DocumentPdf.Header(table, CashLineHeaders);
                foreach (CashTransactionLineResponse line in transaction.Lines ?? [])
                {
                    table.Cell().Element(PdfLayout.Cell).Text(line.LineNumber.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(PdfLayout.Cell).Text($"{line.AccountCode} — {line.AccountName}");
                    table.Cell().Element(PdfLayout.Cell).Text(line.CostCenterCode ?? "—");
                    table.Cell().Element(PdfLayout.Cell).Text(line.Description ?? string.Empty);
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Amount));
                }
            });

            DocumentPdf.Totals(column, [(cashIn ? "Total received" : "Total paid", transaction.Amount, true)]);
            DocumentPdf.InWords(column, transaction.Amount);
            DocumentPdf.Signatures(column, cashIn ? ["Received by", "Approved by", "Paid by"] : ["Prepared by", "Approved by", "Received by"]);
        });
    }
}
