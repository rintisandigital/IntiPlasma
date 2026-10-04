using Application.Access.BranchAccessProfiles;
using Application.Branches.Create;
using Application.Common;
using Application.Contracts;
using Application.Contracts.ChangeStatus;
using Application.Contracts.Create;
using Application.Coops.Create;
using Application.Customers.Create;
using Application.Farmers.Create;
using Application.Finance.Accounts;
using Application.Finance.CashBank;
using Application.Finance.CostCenters;
using Application.Finance.FiscalPeriods;
using Application.Finance.JournalTemplates;
using Application.Items;
using Application.Items.Create;
using Application.TaxCodes;
using Application.TaxCodes.Create;
using Application.Users.Manage;
using Application.Vendors.Create;
using Application.Warehouses.Create;
using Domain.Access;
using Domain.Finance.Accounts;
using Domain.Finance.CashBank;
using Domain.Finance.FiscalPeriods;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Contracts;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.DemoData;

public sealed partial class DemoDataSeeder
{
    public const string CheckerEmail = "checker@intiplasma.local";
    public const string BranchStaffEmail = "staff.bdg@intiplasma.local";

    private readonly List<CycleRun> _cycles = [];
    private readonly Dictionary<Guid, Guid> _baseUoms = [];
    private readonly Dictionary<string, CoopData> _coops = [];
    private Dictionary<string, Guid> _uoms = [];
    private Dictionary<string, Guid> _accounts = [];
    private BranchData _bandung = null!;
    private BranchData _cianjur = null!;
    private Guid _ppn;
    private Guid _ppnExempt;
    private Guid _pph23;
    private Guid _doc;
    private Guid _starterFeed;
    private Guid _finisherFeed;
    private Guid _ndibVaccine;
    private Guid _gumboroVaccine;
    private Guid _vitamin;
    private Guid _liveBird;
    private Guid _vendorCharoen;
    private Guid _vendorJapfa;
    private Guid _vendorMedion;
    private Guid _costCenterProduction;
    private Guid _costCenterAdmin;
    private Guid _costCenterMarketing;

    private async Task SetUpAsync()
    {
        _uoms = await QueryDbAsync(db => db.Uoms.ToDictionaryAsync(u => u.Code, u => u.Id, _ct));
        _accounts = await QueryDbAsync(db => db.Accounts.ToDictionaryAsync(a => a.Code, a => a.Id, _ct));

        await SetUpOrganisationAsync();
        await SetUpFinanceAsync();
        await SetUpMasterDataAsync();
        await SetUpPartnershipAsync();
    }

    private async Task SetUpOrganisationAsync()
    {
        Guid bandung = await SendAsync(new CreateBranchCommand("BDG", "Cabang Bandung", "Jl. Soekarno-Hatta No. 120, Bandung", "022-7501234"));
        Guid cianjur = await SendAsync(new CreateBranchCommand("CJR", "Cabang Cianjur", "Jl. Raya Bandung KM 5, Cianjur", "0263-261234"));

        Guid administratorRoleId = await QueryDbAsync(db => db.Roles
            .Where(r => r.IsSystem && r.Name == Role.AdministratorName)
            .Select(r => r.Id)
            .SingleAsync(_ct));

        // The second user is the checker of every maker-checker flow (journals, cash out, payment vouchers, settlements).
        _checkerId = await SendAsync(new CreateUserCommand(
            CheckerEmail, "Siti", "Rahmawati", DemoPassword,
            MenuAccessProfile.FullAccessId, BranchAccessProfile.AllBranchesId, bandung, [administratorRoleId]));

        Guid bandungOnly = await SendAsync(new CreateBranchAccessProfileCommand(
            "Cabang Bandung", "Hanya data Cabang Bandung (contoh akses cabang)", false, [bandung]));

        await SendAsync(new CreateUserCommand(
            BranchStaffEmail, "Budi", "Santoso", DemoPassword,
            MenuAccessProfile.FullAccessId, bandungOnly, bandung, []));

        _bandung = new BranchData { Id = bandung, Code = "BDG", Plate = "D", Payroll = 86_500_000m, Electricity = 7_450_000m };
        _cianjur = new BranchData { Id = cianjur, Code = "CJR", Plate = "F", Payroll = 61_200_000m, Electricity = 5_180_000m };
    }

    private async Task SetUpFinanceAsync()
    {
        for (int year = _start.Year; year <= _today.Year; year++)
        {
            if (!await QueryDbAsync(db => db.FiscalPeriods.AnyAsync(p => p.Year == year, _ct)))
            {
                await SendAsync(new OpenFiscalYearCommand(year));
            }
        }

        // The empty months before the first transaction are closed, in order (shows the period closing).
        List<Guid> emptyPeriods = await QueryDbAsync(db => db.FiscalPeriods
            .Where(p => p.Year == _start.Year && p.Month < _start.Month && p.Status == FiscalPeriodStatus.Open)
            .OrderBy(p => p.Month)
            .Select(p => p.Id)
            .ToListAsync(_ct));

        foreach (Guid periodId in emptyPeriods)
        {
            await SendAsync(new CloseFiscalPeriodCommand(periodId));
        }

        // Cash/bank accounts each need their own ledger account: Bandung uses the seeded ones, Cianjur gets new ones.
        await SendAsync(new CreateAccountCommand("1-1103", "Kas Besar Cianjur", AccountType.Asset, _accounts["1-1100"], true, null));
        await SendAsync(new CreateAccountCommand("1-1104", "Kas Kecil Cianjur", AccountType.Asset, _accounts["1-1100"], true, null));
        await SendAsync(new CreateAccountCommand("1-1202", "Bank BRI Cianjur", AccountType.Asset, _accounts["1-1200"], true, null));
        _accounts = await QueryDbAsync(db => db.Accounts.ToDictionaryAsync(a => a.Code, a => a.Id, _ct));

        _costCenterProduction = await SendAsync(new CreateCostCenterCommand("PRD", "Produksi"));
        _costCenterAdmin = await SendAsync(new CreateCostCenterCommand("ADM", "Administrasi & Umum"));
        _costCenterMarketing = await SendAsync(new CreateCostCenterCommand("MKT", "Pemasaran"));

        await SendAsync(new CreateJournalTemplateCommand("Penyusutan aset tetap", "Penyusutan bulanan kandang & peralatan inti",
        [
            new JournalTemplateLineRequest(_accounts["6-1301"], _costCenterProduction, BalanceSide.Debit, "Beban penyusutan"),
            new JournalTemplateLineRequest(_accounts["1-2901"], null, BalanceSide.Credit, "Akumulasi penyusutan")
        ]));
        await SendAsync(new CreateJournalTemplateCommand("Biaya dibayar dimuka", "Pembayaran biaya yang belum ditagih",
        [
            new JournalTemplateLineRequest(_accounts["6-1901"], _costCenterAdmin, BalanceSide.Debit, null),
            new JournalTemplateLineRequest(_accounts["2-1401"], null, BalanceSide.Credit, null)
        ]));

        _bandung.CashId = await SendAsync(new CreateCashBankAccountCommand(
            "KAS-BDG", "Kas Besar Bandung", CashBankAccountType.Cash, _bandung.Id, _accounts["1-1101"], null, null));
        _bandung.PettyCashId = await SendAsync(new CreateCashBankAccountCommand(
            "KK-BDG", "Kas Kecil Bandung", CashBankAccountType.PettyCash, _bandung.Id, _accounts["1-1102"], null, null));
        _bandung.BankId = await SendAsync(new CreateCashBankAccountCommand(
            "BCA-BDG", "Bank BCA Bandung", CashBankAccountType.Bank, _bandung.Id, _accounts["1-1201"], "BCA", "7770123456"));
        _cianjur.CashId = await SendAsync(new CreateCashBankAccountCommand(
            "KAS-CJR", "Kas Besar Cianjur", CashBankAccountType.Cash, _cianjur.Id, _accounts["1-1103"], null, null));
        _cianjur.PettyCashId = await SendAsync(new CreateCashBankAccountCommand(
            "KK-CJR", "Kas Kecil Cianjur", CashBankAccountType.PettyCash, _cianjur.Id, _accounts["1-1104"], null, null));
        _cianjur.BankId = await SendAsync(new CreateCashBankAccountCommand(
            "BRI-CJR", "Bank BRI Cianjur", CashBankAccountType.Bank, _cianjur.Id, _accounts["1-1202"], "BRI", "0412-01-000987-30-5"));
    }

    private async Task SetUpMasterDataAsync()
    {
        // Rates as data (decision #5/#11): PPN 12% with "DPP nilai lain" entered as an effective 11% with ratio 1.
        var taxFrom = new DateOnly(2025, 1, 1);
        _ppn = await SendAsync(new CreateTaxCodeCommand(
            "PPN-11", "PPN 12% DPP nilai lain (efektif 11%)", TaxType.Vat, VatTreatment.Taxable, null, [new TaxRateRequest(taxFrom, 11m, 1m)]));
        _ppnExempt = await SendAsync(new CreateTaxCodeCommand(
            "PPN-BBS", "PPN dibebaskan (DOC, pakan, ayam hidup)", TaxType.Vat, VatTreatment.Exempt, null, [new TaxRateRequest(taxFrom, 0m, 1m)]));
        _pph23 = await SendAsync(new CreateTaxCodeCommand(
            "PPH23-2", "PPh Pasal 23 (2%)", TaxType.IncomeTax, null, IncomeTaxArticle.Pph23, [new TaxRateRequest(taxFrom, 2m, 1m)]));

        _doc = await CreateItemAsync("DOC-CP707", "DOC Broiler CP 707", ItemCategory.Doc, "EKOR", _ppnExempt);
        _starterFeed = await CreateItemAsync("PKN-BR1", "Pakan Starter BR-1 Crumble", ItemCategory.Feed, "KG", _ppnExempt, ("SAK", 50m));
        _finisherFeed = await CreateItemAsync("PKN-BR2", "Pakan Finisher BR-2 Pellet", ItemCategory.Feed, "KG", _ppnExempt, ("SAK", 50m));
        _ndibVaccine = await CreateItemAsync("VKS-NDIB", "Vaksin ND-IB Live 1.000 ds", ItemCategory.Ovk, "VIAL", _ppn);
        _gumboroVaccine = await CreateItemAsync("VKS-GMB", "Vaksin Gumboro 1.000 ds", ItemCategory.Ovk, "VIAL", _ppn);
        _vitamin = await CreateItemAsync("VIT-ELK", "Vitamin & Elektrolit 250 gr", ItemCategory.Ovk, "BTL", _ppn);
        _liveBird = await CreateItemAsync("AYAM-HIDUP", "Ayam Broiler Hidup", ItemCategory.LiveBird, "KG", _ppnExempt);
        await CreateItemAsync("SKM-KRG", "Sekam Padi (alas kandang)", ItemCategory.Other, "KG", null);

        _bandung.CentralWarehouseId = await SendAsync(new CreateWarehouseCommand(
            "GI-BDG", "Gudang Induk Bandung", _bandung.Id, "Jl. Soekarno-Hatta No. 120, Bandung"));
        _cianjur.CentralWarehouseId = await SendAsync(new CreateWarehouseCommand(
            "GI-CJR", "Gudang Induk Cianjur", _cianjur.Id, "Jl. Raya Bandung KM 5, Cianjur"));

        _vendorCharoen = await SendAsync(new CreateVendorCommand(
            "V-CPI", "PT Charoen Pokphand Indonesia", Pkp("0013090721092000"), "Jl. Ancol VIII No. 1, Jakarta Utara",
            "021-6919999", "sales@cp.example.co.id", 30, new BankAccountRequest("BCA", "0353012345", "PT Charoen Pokphand Indonesia")));
        _vendorJapfa = await SendAsync(new CreateVendorCommand(
            "V-JPF", "PT Japfa Comfeed Indonesia", Pkp("0010615074092000"), "Jl. Daan Mogot KM 12, Jakarta Barat",
            "021-28545680", "order@japfa.example.co.id", 30, new BankAccountRequest("Mandiri", "1180009876543", "PT Japfa Comfeed Indonesia")));
        _vendorMedion = await SendAsync(new CreateVendorCommand(
            "V-MDN", "PT Medion Farma Jaya", Pkp("0013245698441000"), "Jl. Babakan Ciparay No. 282, Bandung",
            "022-6036000", "cs@medion.example.co.id", 14, new BankAccountRequest("BNI", "0098765432", "PT Medion Farma Jaya"), 2m));
        await SendAsync(new CreateVendorCommand(
            "V-SKM", "UD Sekam Jaya", NonPkp(), "Kp. Cibeber, Cianjur", "0812-2233-4455", null, 0,
            new BankAccountRequest("BRI", "4123-01-002233-50-1", "Ujang Sekam")));

        _customers.Rpa = await SendAsync(new CreateCustomerCommand(
            "C-RPA-SB", "PT Sumber Berkah Unggas (RPA)", Pkp("0213456789428000"), "Jl. Raya Cileunyi No. 45, Bandung",
            "022-7798811", "purchasing@sbu.example.co.id", 14, 1_500_000_000m));
        _customers.Broker = await SendAsync(new CreateCustomerCommand(
            "C-BKL-JA", "UD Jaya Abadi (Bakul)", NonPkp(), "Pasar Ciroyom Blok C-12, Bandung",
            "0813-2111-7788", null, 7, 400_000_000m));
        _customers.Cianjur = await SendAsync(new CreateCustomerCommand(
            "C-UMC", "CV Unggas Makmur Cianjur", Pkp("0314567890406000"), "Jl. Dr. Muwardi No. 18, Cianjur",
            "0263-270011", "admin@umc.example.co.id", 14, 900_000_000m));
        await SendAsync(new CreateCustomerCommand(
            "C-WRG-01", "Warung Ayam Bu Imas", NonPkp(), "Jl. Siliwangi No. 3, Cianjur", "0857-1122-3344", null, 0, 0m));
    }

    private async Task SetUpPartnershipAsync()
    {
        Guid bandungInti = await CreateFarmerAsync("PTN-BDG-INTI", "Farm Inti Lembang", FarmerType.Inti, _bandung, null, "Jl. Raya Lembang KM 9, Bandung Barat");
        Guid ahmad = await CreateFarmerAsync("PTN-BDG-001", "H. Ahmad Suryadi", FarmerType.Plasma, _bandung, "3204011203750001", "Kp. Cikoneng RT 02/05, Bojongsoang");
        Guid dedi = await CreateFarmerAsync("PTN-BDG-002", "Dedi Kurniawan", FarmerType.Plasma, _bandung, "3204022508820003", "Ds. Cilame RT 01/03, Ngamprah");
        Guid asep = await CreateFarmerAsync("PTN-BDG-003", "Asep Saepudin", FarmerType.Plasma, _bandung, "3204031707880002", "Ds. Pangalengan RT 04/01, Pangalengan");
        Guid cianjurInti = await CreateFarmerAsync("PTN-CJR-INTI", "Farm Inti Cipanas", FarmerType.Inti, _cianjur, null, "Jl. Raya Cipanas No. 77, Cianjur");
        Guid ujang = await CreateFarmerAsync("PTN-CJR-001", "Ujang Hermawan", FarmerType.Plasma, _cianjur, "3203010505800004", "Kp. Sukamaju RT 03/02, Cugenang");
        Guid nining = await CreateFarmerAsync("PTN-CJR-002", "Nining Sumarni", FarmerType.Plasma, _cianjur, "3203024411850001", "Ds. Sukaresmi RT 01/04, Sukaresmi");

        await CreateCoopAsync(bandungInti, _bandung, "KDG-BDG-INTI", "Kandang Inti Lembang", 10_000, HouseType.ClosedHouse, -6.8132m, 107.6175m);
        await CreateCoopAsync(ahmad, _bandung, "KDG-BDG-01", "Kandang Ahmad 1", 6_000, HouseType.ClosedHouse, -6.9765m, 107.6402m);
        await CreateCoopAsync(ahmad, _bandung, "KDG-BDG-02", "Kandang Ahmad 2", 7_000, HouseType.ClosedHouse, -6.9771m, 107.6418m);
        await CreateCoopAsync(dedi, _bandung, "KDG-BDG-03", "Kandang Dedi", 5_000, HouseType.OpenHouse, -6.8820m, 107.5194m);
        await CreateCoopAsync(asep, _bandung, "KDG-BDG-04", "Kandang Asep", 4_000, HouseType.OpenHouse, -7.1763m, 107.5713m);
        await CreateCoopAsync(cianjurInti, _cianjur, "KDG-CJR-INTI", "Kandang Inti Cipanas", 8_000, HouseType.ClosedHouse, -6.7350m, 107.0412m);
        await CreateCoopAsync(ujang, _cianjur, "KDG-CJR-01", "Kandang Ujang", 5_000, HouseType.ClosedHouse, -6.8051m, 107.1003m);
        await CreateCoopAsync(nining, _cianjur, "KDG-CJR-02", "Kandang Nining", 4_000, HouseType.OpenHouse, -6.7012m, 107.0498m);

        DateOnly validFrom = new(_start.Year, _start.Month, 1);
        Guid bandungPrice = await CreateContractAsync("KTR-BDG-HK26", _bandung, ContractScheme.PriceContract, "Harga Kontrak Bandung 2026", validFrom);
        Guid bandungShare = await CreateContractAsync("KTR-BDG-BH26", _bandung, ContractScheme.ProfitSharing, "Bagi Hasil Bandung 2026", validFrom);
        Guid cianjurPrice = await CreateContractAsync("KTR-CJR-HK26", _cianjur, ContractScheme.PriceContract, "Harga Kontrak Cianjur 2026", validFrom);

        DefineCycles(bandungPrice, bandungShare, cianjurPrice);
    }

    /// <summary>
    /// Eight cycles, chick-in dated relative to today, so every stage of the cycle is present whenever the seed runs.
    /// </summary>
    private void DefineCycles(Guid bandungPrice, Guid bandungShare, Guid cianjurPrice)
    {
        // Settled and paid; with a revised recording and a credit note (weighing shrinkage claim).
        AddCycle(new CycleSpec("KDG-BDG-01", bandungPrice, 5_000, 80, [33, 34, 35], _customers.Rpa, 21_800m)
        {
            FeedFactor = 0.93m, GrowthFactor = 1.01m, Receipts = ReceiptPlan.Full, Settlement = SettlementPlan.Paid,
            ReviseRecording = true, CreditNote = true
        });

        // Profit sharing, partly paid by the customer, settlement waiting for the checker; leftover feed moved to KDG-BDG-02.
        AddCycle(new CycleSpec("KDG-BDG-03", bandungShare, 4_500, 70, [32, 34], _customers.Broker, 21_300m)
        {
            FeedFactor = 1.03m, GrowthFactor = 0.98m, MortalityFactor = 1.2m, FeedVendor = CycleVendor.Japfa,
            Receipts = ReceiptPlan.Partial, Settlement = SettlementPlan.Draft, MoveLeftoverFeedTo = "KDG-BDG-02"
        });

        // Harvesting: two of four trucks out, the last one not invoiced yet.
        AddCycle(new CycleSpec("KDG-BDG-02", bandungPrice, 6_000, 35, [33, 34, 35, 36], _customers.Rpa, 22_100m)
        {
            FeedFactor = 0.98m, GrowthFactor = 1.02m, MortalityFactor = 0.9m, Receipts = ReceiptPlan.Full
        });

        // Inti, growing.
        AddCycle(new CycleSpec("KDG-BDG-INTI", null, 8_000, 21, [35, 36, 37], _customers.Rpa, 22_000m) { FeedVendor = CycleVendor.Japfa });

        // Planned: chick-in in three days, purchase orders approved, nothing received yet.
        AddCycle(new CycleSpec("KDG-BDG-04", bandungPrice, 4_000, -3, [35, 36], _customers.Broker, 21_500m));

        // Poor performance (FCR, depletion → deduction), settled; the plasma payment voucher waits to be paid.
        AddCycle(new CycleSpec("KDG-CJR-01", cianjurPrice, 5_000, 66, [34, 35, 36], _customers.Cianjur, 21_500m)
        {
            FeedFactor = 1.07m, GrowthFactor = 0.97m, MortalityFactor = 1.6m, Receipts = ReceiptPlan.Full, Settlement = SettlementPlan.VoucherApproved
        });

        // Inti, closed; customer paid an advance first, the rest partly.
        AddCycle(new CycleSpec("KDG-CJR-INTI", null, 7_000, 42, [35, 36, 37], _customers.Cianjur, 21_900m)
        {
            Receipts = ReceiptPlan.Partial, AdvanceAmount = 50_000_000m, FeedVendor = CycleVendor.Japfa
        });

        // Plasma, growing; supplier invoices still unpaid.
        AddCycle(new CycleSpec("KDG-CJR-02", cianjurPrice, 3_500, 12, [35, 36], _customers.Cianjur, 21_700m) { MortalityFactor = 1.1m });
    }

    private void AddCycle(CycleSpec spec) => _cycles.Add(new CycleRun(this, spec, _cycles.Count + 1));

    private async Task<Guid> CreateItemAsync(
        string code, string name, ItemCategory category, string baseUom, Guid? taxCodeId, params (string Uom, decimal Factor)[] conversions)
    {
        Guid id = await SendAsync(new CreateItemCommand(
            code, name, category, _uoms[baseUom], taxCodeId,
            [.. conversions.Select(c => new ItemUomConversionRequest(_uoms[c.Uom], c.Factor))]));

        _baseUoms[id] = _uoms[baseUom];

        return id;
    }

    private Task<Guid> CreateFarmerAsync(string code, string name, FarmerType type, BranchData branch, string? nik, string address) =>
        SendAsync(new CreateFarmerCommand(
            code, name, type, branch.Id, nik, NonPkp(), address, "0812-" + code[^3..] + "0-1234",
            new BankAccountRequest("BRI", "3301-01-" + code[^3..] + "123-50-7", name)));

    private async Task CreateCoopAsync(
        Guid farmerId, BranchData branch, string code, string name, int capacity, HouseType houseType, decimal latitude, decimal longitude)
    {
        Guid coopId = await SendAsync(new CreateCoopCommand(farmerId, code, name, capacity, houseType, null, latitude, longitude));

        // The coop warehouse (GK-{code}) is created by the outbox handler of CoopCreated, already processed.
        Guid warehouseId = await QueryDbAsync(db => db.Warehouses.Where(w => w.CoopId == coopId).Select(w => w.Id).SingleAsync(_ct));

        _coops[code] = new CoopData(coopId, warehouseId, farmerId, branch);
    }

    private async Task<Guid> CreateContractAsync(string code, BranchData branch, ContractScheme scheme, string name, DateOnly validFrom)
    {
        bool priceContract = scheme == ContractScheme.PriceContract;

        // Sapronak at the contract price (per base unit) and guaranteed live-bird prices per average-weight band [min, max).
        ContractTermsRequest.InputPrice[] inputPrices = priceContract
            ?
            [
                new(_doc, 7_900m), new(_starterFeed, 8_600m), new(_finisherFeed, 8_300m),
                new(_ndibVaccine, 100_000m), new(_gumboroVaccine, 118_000m), new(_vitamin, 47_500m)
            ]
            : [];
        ContractTermsRequest.LiveBirdPrice[] liveBirdPrices = priceContract
            ?
            [
                new(0.5m, 1.6m, 21_000m), new(1.6m, 1.8m, 20_600m), new(1.8m, 2.0m, 20_300m),
                new(2.0m, 2.2m, 20_000m), new(2.2m, 5.0m, 19_600m)
            ]
            : [];
        ContractTermsRequest.Incentive[] incentives =
        [
            new("Bonus FCR ≤ 1,50", IncentiveKind.Bonus, IncentiveMetric.Fcr, 0m, 1.50m, 150m, IncentiveBasis.PerKg),
            new("Bonus IP ≥ 380", IncentiveKind.Bonus, IncentiveMetric.Ip, 380m, 999m, 100m, IncentiveBasis.PerKg),
            new("Potongan deplesi > 6%", IncentiveKind.Deduction, IncentiveMetric.Depletion, 6.01m, 100m, 150m, IncentiveBasis.PerBird)
        ];

        Guid contractId = await SendAsync(new CreateContractCommand(
            code, branch.Id, scheme,
            new ContractTermsRequest(
                name, validFrom, validFrom.AddYears(1).AddDays(-1), priceContract ? null : 40m, _pph23,
                priceContract ? "Harga sapronak & jaminan harga ayam per rentang BW" : "Bagi hasil 40% laba siklus untuk plasma; rugi ditanggung inti",
                inputPrices, liveBirdPrices, incentives)));

        await SendAsync(new ActivateContractCommand(contractId));

        return contractId;
    }

    private static TaxIdentityRequest Pkp(string npwp) => new(npwp, npwp + "000000", true);

    private static TaxIdentityRequest NonPkp() => new(null, null, false);

    private readonly CustomerIds _customers = new();

    private sealed class CustomerIds
    {
        public Guid Rpa { get; set; }

        public Guid Broker { get; set; }

        public Guid Cianjur { get; set; }
    }

    private sealed class BranchData
    {
        public required Guid Id { get; init; }

        public required string Code { get; init; }

        /// <summary>
        /// Vehicle registration prefix (D = Bandung, F = Cianjur).
        /// </summary>
        public required string Plate { get; init; }

        public required decimal Payroll { get; init; }

        public required decimal Electricity { get; init; }

        public Guid CentralWarehouseId { get; set; }

        public Guid CashId { get; set; }

        public Guid PettyCashId { get; set; }

        public Guid BankId { get; set; }
    }

    private sealed record CoopData(Guid Id, Guid WarehouseId, Guid FarmerId, BranchData Branch);
}
