using System.Globalization;
using Application.Finance.Receivables;
using Application.Sales;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Sales.Documents;

/// <summary>
/// Printed sales documents (PLAN-WEBAPP §5.3): delivery note, invoice, credit note and receipt. English, with the
/// amount in words in Indonesian on invoices, credit notes and receipts (W-17).
/// </summary>
public static class SalesDocumentPdf
{
    private static readonly string[] DeliveryHeaders = ["#", "Item", "Harvest", "Cycle / coop", "Birds", "Weight (kg)", "Avg (kg)"];
    private static readonly string[] InvoiceHeaders = ["#", "Delivery", "Item", "Birds", "Weight (kg)", "Price/kg", "Amount", "VAT"];

    public static void DeliveryNote(IContainer container, DeliveryOrderResponse delivery, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(fmt);

        DocumentPdf.Compose(container, column =>
        {
            column.Item().Element(c => DocumentPdf.Fields(c,
            [
                ("Customer", $"{delivery.CustomerName} ({delivery.CustomerCode})"),
                ("Delivery note", delivery.Number),
                ("Sales order", delivery.SalesOrderNumber),
                ("Delivery date", fmt.Date(delivery.DeliveryDate)),
                ("Vehicle", delivery.VehicleNumber ?? "—"),
                ("Driver", delivery.DriverName ?? "—")
            ]));

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(2.6f);
                    c.RelativeColumn(1);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(0.9f);
                });
                DocumentPdf.Header(table, DeliveryHeaders);
                foreach (DeliveryOrderLineResponse line in delivery.Lines ?? [])
                {
                    table.Cell().Element(PdfLayout.Cell).Text(line.LineNumber.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(PdfLayout.Cell).Text($"{line.ItemCode} — {line.ItemName}");
                    table.Cell().Element(PdfLayout.Cell).Text(fmt.Date(line.HarvestDate));
                    table.Cell().Element(PdfLayout.Cell).Text($"{line.CycleNumber} · {line.CoopName}");
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Birds));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.WeightKg, 2));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.AverageWeightKg, 3));
                }

                table.Cell().ColumnSpan(4).Element(PdfLayout.Cell).AlignRight().Text("Total").Bold();
                table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(delivery.Birds)).Bold();
                table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(delivery.WeightKg, 2)).Bold();
                table.Cell().Element(PdfLayout.Cell).Text(string.Empty);
            });

            DocumentPdf.Notes(column, delivery.Notes);
            DocumentPdf.Signatures(column, ["Issued by", "Driver", "Received by (customer)"]);
        });
    }

    public static void Invoice(IContainer container, SalesInvoiceResponse invoice, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(fmt);

        DocumentPdf.Compose(container, column =>
        {
            column.Item().Element(c => DocumentPdf.Fields(c,
            [
                ("Bill to", $"{invoice.CustomerName} ({invoice.CustomerCode})"),
                ("Invoice", invoice.Number ?? "DRAFT"),
                ("Invoice date", fmt.Date(invoice.InvoiceDate)),
                ("Due date", fmt.Date(invoice.DueDate)),
                ("Branch", invoice.BranchCode),
                ("Status", EnumOptions.Label(invoice.Status))
            ]));

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(0.8f);
                });
                DocumentPdf.Header(table, InvoiceHeaders);
                foreach (SalesInvoiceLineResponse line in invoice.Lines ?? [])
                {
                    table.Cell().Element(PdfLayout.Cell).Text(line.LineNumber.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(PdfLayout.Cell).Text(line.DeliveryOrderNumber);
                    table.Cell().Element(PdfLayout.Cell).Text($"{line.ItemCode} — {line.ItemName}");
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Birds));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.WeightKg, 2));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.PricePerKg));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Amount));
                    table.Cell().Element(PdfLayout.Cell).Text(line.TaxCode ?? "—");
                }
            });

            DocumentPdf.Totals(column,
            [
                ("Subtotal", invoice.Subtotal, false),
                ("VAT", invoice.VatAmount, false),
                ("Total", invoice.Total, true),
                ("Paid", invoice.PaidAmount, false),
                ("Credited", invoice.CreditedAmount, false),
                ("Outstanding", invoice.Outstanding, true)
            ]);
            DocumentPdf.InWords(column, invoice.Total);
            DocumentPdf.Notes(column, invoice.Notes);
            DocumentPdf.Signatures(column, ["Finance", "Received by (customer)"]);
        });
    }

    public static void CreditNote(IContainer container, SalesCreditNoteResponse note, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(fmt);

        DocumentPdf.Compose(container, column =>
        {
            column.Item().Element(c => DocumentPdf.Fields(c,
            [
                ("Customer", note.CustomerName),
                ("Credit note", note.Number),
                ("Date", fmt.Date(note.Date)),
                ("Invoice", note.InvoiceNumber),
                ("Branch", note.BranchCode),
                ("Reason", note.Reason)
            ]));
            DocumentPdf.Totals(column,
            [
                ("Reduction (excl. VAT)", note.Subtotal, false),
                ("VAT correction", note.VatAmount, false),
                ("Total credited", note.Total, true)
            ]);
            DocumentPdf.InWords(column, note.Total);
            DocumentPdf.Signatures(column, ["Finance", "Customer"]);
        });
    }

    public static void Receipt(IContainer container, CustomerReceiptResponse receipt, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(fmt);

        DocumentPdf.Compose(container, column =>
        {
            column.Item().Element(c => DocumentPdf.Fields(c,
            [
                ("Received from", $"{receipt.CustomerName} ({receipt.CustomerCode})"),
                ("Receipt", receipt.Number),
                ("Date", fmt.Date(receipt.ReceiptDate)),
                ("Into", $"{receipt.CashBankCode ?? receipt.CashAccountCode} — {receipt.CashAccountName}"),
                ("Reference", receipt.Reference ?? "—"),
                ("Status", receipt.Status == "Voided" ? $"Voided {receipt.VoidDate} — {receipt.VoidReason}" : receipt.Status)
            ]));

            if (receipt.Allocations is { Count: > 0 } allocations)
            {
                column.Item().Text("Payment of invoices").Bold().FontSize(10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(1.5f);
                    });
                    DocumentPdf.Header(table, ["Invoice", "Invoice date", "Amount"]);
                    foreach (CustomerReceiptAllocationResponse allocation in allocations)
                    {
                        table.Cell().Element(PdfLayout.Cell).Text(allocation.InvoiceNumber);
                        table.Cell().Element(PdfLayout.Cell).Text(fmt.Date(allocation.InvoiceDate));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(allocation.Amount));
                    }
                });
            }

            DocumentPdf.Totals(column,
            [
                ("Advance (uang muka)", receipt.AdvanceAmount, false),
                ("Total received", receipt.Amount, true)
            ]);
            DocumentPdf.InWords(column, receipt.Amount);
            DocumentPdf.Notes(column, receipt.Notes);
            DocumentPdf.Signatures(column, ["Received by", "Customer"]);
        });
    }
}
