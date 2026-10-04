using Application.Finance.CashBank;
using Application.Finance.Journals;
using Application.Procurement;
using Domain.Finance.CashBank;

namespace Infrastructure.Database.DemoData;

public sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Company-level routine per branch: opening balances, petty cash top-ups, utilities, payroll, field expenses,
    /// scrap sales and month-end depreciation.
    /// </summary>
    private async Task RunFinanceDayAsync(DateOnly date)
    {
        bool lastDayOfMonth = date.AddDays(1).Day == 1;

        foreach (BranchData branch in new[] { _bandung, _cianjur })
        {
            if (date == _start)
            {
                await OpenBalancesAsync(branch, date);
                continue;
            }

            if (date.Day == 1)
            {
                await SendAsync(new CreateBankTransferCommand(branch.BankId, branch.PettyCashId, date, 10_000_000m, null, "Pengisian kas kecil"));
            }

            if (date.Day == 10)
            {
                await CashOutAsync(branch.BankId, date, "Pembayaran listrik & air kantor dan kandang inti", "PLN/PDAM",
                    ("6-1401", _costCenterProduction, branch.Electricity));
            }

            if (date.Day == 25)
            {
                await CashOutAsync(branch.BankId, date, "Gaji & tunjangan karyawan", "Payroll",
                    ("6-1101", _costCenterAdmin, branch.Payroll * 0.65m),
                    ("6-1101", _costCenterProduction, branch.Payroll * 0.35m));
            }

            if (date.DayOfWeek == DayOfWeek.Monday)
            {
                await CashOutAsync(branch.PettyCashId, date, "BBM & transport PPL lapangan", null,
                    ("6-1201", _costCenterProduction, 1_150_000m + date.DayOfYear % 7 * 85_000m),
                    ("6-1901", _costCenterAdmin, 235_000m + date.DayOfYear % 5 * 40_000m));
            }

            if (date.Day == 15)
            {
                Guid cashIn = await SendAsync(new CreateCashTransactionCommand(
                    branch.CashId, CashDirection.In, date, "Penjualan karung pakan bekas", null,
                    [new CashTransactionLineRequest(_accounts["7-1901"], _costCenterMarketing, "Karung bekas", 1_450_000m)]));
                await SendAsync(new PostCashTransactionCommand(cashIn));
            }

            if (lastDayOfMonth)
            {
                await PostJournalAsync(branch, date, "Penyusutan aset tetap bulanan",
                    new JournalLineRequest(_accounts["6-1301"], _costCenterProduction, "Beban penyusutan", branch.Payroll / 7, 0m),
                    new JournalLineRequest(_accounts["1-2901"], null, "Akumulasi penyusutan", 0m, branch.Payroll / 7));
            }
        }
    }

    /// <summary>
    /// Paid-in capital as bank, cash and coop buildings/equipment (manual journal, maker-checker), then petty cash.
    /// </summary>
    private async Task OpenBalancesAsync(BranchData branch, DateOnly date)
    {
        decimal scale = branch == _bandung ? 1m : 0.75m;
        decimal bank = 2_500_000_000m * scale;
        decimal cash = 50_000_000m * scale;
        decimal buildings = 1_200_000_000m * scale;
        decimal equipment = 350_000_000m * scale;

        await PostJournalAsync(branch, date, "Saldo awal: setoran modal",
            new JournalLineRequest(_accounts[branch == _bandung ? "1-1201" : "1-1202"], null, "Saldo bank", bank, 0m),
            new JournalLineRequest(_accounts[branch == _bandung ? "1-1101" : "1-1103"], null, "Saldo kas besar", cash, 0m),
            new JournalLineRequest(_accounts["1-2201"], null, "Bangunan kandang inti", buildings, 0m),
            new JournalLineRequest(_accounts["1-2301"], null, "Peralatan kandang inti", equipment, 0m),
            new JournalLineRequest(_accounts["3-1101"], null, "Modal disetor", 0m, bank + cash + buildings + equipment));

        await SendAsync(new CreateBankTransferCommand(branch.BankId, branch.PettyCashId, date, 15_000_000m, null, "Saldo awal kas kecil"));
    }

    private async Task PostJournalAsync(BranchData branch, DateOnly date, string description, params JournalLineRequest[] lines)
    {
        Guid journalId = await SendAsync(new CreateJournalCommand(branch.Id, date, description, lines));
        await SendAsAsync(_checkerId, new ApproveJournalCommand(journalId));
        await SendAsync(new PostJournalCommand(journalId));
    }

    /// <summary>
    /// Cash out: made by the admin, approved by the checker, posted by the admin.
    /// </summary>
    private async Task<Guid> CashOutAsync(
        Guid cashBankAccountId, DateOnly date, string description, string? reference, params (string Account, Guid CostCenter, decimal Amount)[] lines)
    {
        Guid id = await SendAsync(new CreateCashTransactionCommand(
            cashBankAccountId, CashDirection.Out, date, description, reference,
            [.. lines.Select(l => new CashTransactionLineRequest(_accounts[l.Account], l.CostCenter, null, Math.Round(l.Amount, 0)))]));

        await SendAsAsync(_checkerId, new ApproveCashTransactionCommand(id));
        await SendAsync(new PostCashTransactionCommand(id));

        return id;
    }

    /// <summary>
    /// After the simulation: a completed bank reconciliation for last month, and some open work for the inboxes
    /// (draft journal, cash out waiting for approval, draft purchase order).
    /// </summary>
    private async Task FinishAsync()
    {
        DateOnly statementDate = new DateOnly(_today.Year, _today.Month, 1).AddDays(-1);

        if (statementDate > _start)
        {
            await ReconcileAsync(_bandung.BankId, statementDate);
        }

        DateOnly yesterday = _today.AddDays(-1);

        await SendAsync(new CreateJournalCommand(_bandung.Id, yesterday, "Reklasifikasi biaya ATK ke beban umum",
        [
            new JournalLineRequest(_accounts["6-1901"], _costCenterAdmin, "ATK kantor", 1_275_000m, 0m),
            new JournalLineRequest(_accounts["6-1201"], _costCenterProduction, "Salah akun", 0m, 1_275_000m)
        ]));

        await SendAsync(new CreateCashTransactionCommand(
            _cianjur.PettyCashId, CashDirection.Out, yesterday, "Pembelian sekam padi untuk alas kandang", null,
            [new CashTransactionLineRequest(_accounts["6-1901"], _costCenterProduction, "Sekam 2 truk", 2_400_000m)]));

        await SendAsync(new CreatePurchaseOrderCommand(
            _bandung.Id, _vendorJapfa, yesterday, _today.AddDays(5), "Stok pakan gudang induk (draft)",
            [
                new PurchaseOrderLineRequest(_starterFeed, _uoms["SAK"], 120, 8_100m * 50, _ppnExempt),
                new PurchaseOrderLineRequest(_finisherFeed, _uoms["SAK"], 300, 7_850m * 50, _ppnExempt)
            ]));
    }

    /// <summary>
    /// Bank statement = the book entries up to the statement date, matched automatically and completed.
    /// </summary>
    private async Task ReconcileAsync(Guid bankId, DateOnly statementDate)
    {
        Guid reconciliationId = await SendAsync(new StartBankReconciliationCommand(bankId, statementDate, 0m));

        BankReconciliationResponse reconciliation = await QueryAsync(new GetBankReconciliationByIdQuery(reconciliationId));

        StatementLineRequest[] lines =
            [.. reconciliation.UnclearedEntries.Select(e => new StatementLineRequest(e.Date, e.Number + " " + e.Description, e.Amount))];

        await SendAsync(new AddStatementLinesCommand(reconciliationId, lines));
        await SendAsync(new UpdateStatementBalanceCommand(reconciliationId, lines.Sum(l => l.Amount)));
        await SendAsync(new AutoMatchStatementLinesCommand(reconciliationId));
        await SendAsync(new CompleteBankReconciliationCommand(reconciliationId));
    }
}
