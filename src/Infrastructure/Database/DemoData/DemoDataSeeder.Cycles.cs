using Application.Costing;
using Application.Cycles.Plan;
using Application.Cycles.Start;
using Application.Finance.Payables;
using Application.Finance.Receivables;
using Application.Inventory.GoodsReceipts;
using Application.Inventory.StockReturns;
using Application.Inventory.StockTransfers;
using Application.Procurement;
using Application.Production;
using Application.Sales;
using Domain.Costing.PlasmaSettlements;
using Domain.Finance.Payables;
using Domain.Sales.SalesInvoices;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.DemoData;

public sealed partial class DemoDataSeeder
{
    private static readonly string[] Drivers = ["Ade Supriatna", "Cecep Hidayat", "Jajang Nurjaman", "Mamat Rahmat", "Yayan Sopyan"];

    private int _vendorInvoiceSequence;
    private int _truckSequence;

    private enum ReceiptPlan
    {
        None,
        Partial,
        Full
    }

    private enum SettlementPlan
    {
        None,
        Draft,
        Approved,
        VoucherApproved,
        Paid
    }

    private enum CycleVendor
    {
        Charoen,
        Japfa
    }

    /// <param name="ChickInDaysAgo">Chick-in date as days before today (negative: still planned).</param>
    /// <param name="HarvestAges">Age (days) of each harvest truck; only the ones before today happen.</param>
    private sealed record CycleSpec(
        string CoopCode,
        Guid? ContractId,
        int Population,
        int ChickInDaysAgo,
        int[] HarvestAges,
        Guid CustomerId,
        decimal PricePerKg)
    {
        public decimal FeedFactor { get; init; } = 1m;

        public decimal GrowthFactor { get; init; } = 1m;

        public decimal MortalityFactor { get; init; } = 1m;

        public CycleVendor FeedVendor { get; init; } = CycleVendor.Charoen;

        public ReceiptPlan Receipts { get; init; }

        public SettlementPlan Settlement { get; init; }

        public bool ReviseRecording { get; init; }

        public bool CreditNote { get; init; }

        public decimal AdvanceAmount { get; init; }

        public string? MoveLeftoverFeedTo { get; init; }
    }

    private sealed record ReceivedGoods(Guid GoodsReceiptId, Guid VendorId, string VendorPrefix, (decimal Quantity, decimal UnitPrice)[] Lines, bool Taxable, int InvoiceAge);

    /// <summary>
    /// One production cycle simulated day by day: purchasing → chick-in → recordings → harvest & sales → close → settlement.
    /// </summary>
    private sealed class CycleRun
    {
        private const int BranchVendorPaymentAge = 20;
        private const int ReceiptLagDays = 3;

        private readonly DemoDataSeeder _s;
        private readonly CycleSpec _spec;
        private readonly CoopData _coop;
        private readonly DateOnly _chickIn;
        private readonly int _firstHarvestAge;
        private readonly int _lastHarvestAge;
        private readonly List<ReceivedGoods> _receipts = [];
        private readonly List<(Guid Id, Guid VendorId)> _vendorInvoices = [];
        private readonly Dictionary<int, Guid> _deliveries = [];
        private readonly Dictionary<int, Guid> _invoices = [];
        private uint _random;
        private Guid _cycleId;
        private int _population;
        private Guid _salesOrderId;
        private Guid _advanceReceiptId;
        private Guid _settlementId;
        private Guid _recordingToRevise;
        private CreateDailyRecordingCommand? _revisedRecording;

        public CycleRun(DemoDataSeeder seeder, CycleSpec spec, int index)
        {
            _s = seeder;
            _spec = spec;
            _coop = seeder._coops[spec.CoopCode];
            _chickIn = seeder._today.AddDays(-spec.ChickInDaysAgo);
            _firstHarvestAge = spec.HarvestAges.Min();
            _lastHarvestAge = spec.HarvestAges.Max();
            _random = (uint)(index * 7919);
        }

        private BranchData Branch => _coop.Branch;

        private int Vials => (_spec.Population + 999) / 1000;

        private int VitaminBottles => (_spec.Population + 1999) / 2000;

        public async Task RunDayAsync(DateOnly date)
        {
            int age = date.DayNumber - _chickIn.DayNumber;

            if (age == -4)
            {
                await PlanAndOrderAsync(date);
            }

            if (_cycleId == Guid.Empty)
            {
                return;
            }

            await RunPurchasingAsync(date, age);

            if (age == _spec.HarvestAges.Min() - 2)
            {
                await OrderSalesAsync(date);
            }

            if (age == 11 && _revisedRecording is not null)
            {
                await ReviseRecordingAsync();
            }

            if (age >= 1 && _population > 0)
            {
                await RecordDayAsync(date, age);
            }

            if (_spec.HarvestAges.Contains(age))
            {
                await HarvestAsync(date, age);
            }

            await RunReceivablesAsync(date, age);

            if (age == _lastHarvestAge + 2)
            {
                await CloseAsync(date);
            }

            if (age == _lastHarvestAge + 4 && _spec.Settlement != SettlementPlan.None)
            {
                await SettleAsync(date);
            }

            if (age == _lastHarvestAge + 6 && _spec.Settlement >= SettlementPlan.VoucherApproved)
            {
                await PayPlasmaAsync(date);
            }
        }

        private async Task RunPurchasingAsync(DateOnly date, int age)
        {
            if (age == -2)
            {
                await ReceiveAtCentralAsync(date);
            }

            if (age == 0)
            {
                await ChickInAsync(date);
            }

            if (age == 12)
            {
                await TransferAsync(date, [(_s._finisherFeed, FeedSacks(15, _lastHarvestAge))], "Kirim pakan finisher BR-2");
            }

            foreach (ReceivedGoods goods in _receipts.Where(r => r.InvoiceAge == age))
            {
                await InvoiceVendorAsync(date, goods);
            }

            if (age == BranchVendorPaymentAge)
            {
                await PayVendorsAsync(date);
            }
        }

        // ---- Purchasing & stock ------------------------------------------------------------------------------------

        private async Task PlanAndOrderAsync(DateOnly date)
        {
            PlanCycleResponse cycle = await _s.SendAsync(new PlanCycleCommand(
                _coop.Id, _spec.ContractId, _chickIn, _spec.Population, "Rencana chick-in DOC CP 707"));
            _cycleId = cycle.Id;

            Guid feedVendor = _spec.FeedVendor == CycleVendor.Japfa ? _s._vendorJapfa : _s._vendorCharoen;
            decimal starterPrice = _spec.FeedVendor == CycleVendor.Japfa ? 8_100m : 8_150m;
            decimal finisherPrice = _spec.FeedVendor == CycleVendor.Japfa ? 7_850m : 7_900m;
            Guid sack = _s._uoms["SAK"];

            await OrderAsync(date, _s._vendorCharoen, $"DOC siklus {cycle.Number}",
                [new PurchaseOrderLineRequest(_s._doc, _s._uoms["EKOR"], _spec.Population, 7_400m, _s._ppnExempt)]);
            await OrderAsync(date, feedVendor, $"Pakan siklus {cycle.Number}",
            [
                new PurchaseOrderLineRequest(_s._starterFeed, sack, FeedSacks(1, 14), starterPrice * 50, _s._ppnExempt),
                new PurchaseOrderLineRequest(_s._finisherFeed, sack, FeedSacks(15, _lastHarvestAge), finisherPrice * 50, _s._ppnExempt)
            ]);
            await OrderAsync(date, _s._vendorMedion, $"OVK siklus {cycle.Number}",
            [
                new PurchaseOrderLineRequest(_s._ndibVaccine, _s._uoms["VIAL"], Vials * 2 + 1, 95_000m, _s._ppn),
                new PurchaseOrderLineRequest(_s._gumboroVaccine, _s._uoms["VIAL"], Vials, 112_000m, _s._ppn),
                new PurchaseOrderLineRequest(_s._vitamin, _s._uoms["BTL"], VitaminBottles * 5 + 1, 42_000m, _s._ppn)
            ]);
        }

        private readonly List<(Guid Id, Guid VendorId, PurchaseOrderLineRequest[] Lines)> _orders = [];

        private async Task OrderAsync(DateOnly date, Guid vendorId, string notes, PurchaseOrderLineRequest[] lines)
        {
            CreatePurchaseOrderResponse order = await _s.SendAsync(new CreatePurchaseOrderCommand(
                Branch.Id, vendorId, date, _chickIn.AddDays(-1), notes, lines));

            await _s.SendAsAsync(_s._checkerId, new ApprovePurchaseOrderCommand(order.Id));

            _orders.Add((order.Id, vendorId, lines));
        }

        /// <summary>
        /// Feed and OVK into the central warehouse two days before chick-in; DOC goes straight to the coop warehouse.
        /// </summary>
        private async Task ReceiveAtCentralAsync(DateOnly date)
        {
            foreach ((Guid id, Guid vendorId, PurchaseOrderLineRequest[] lines) in _orders.Skip(1))
            {
                await ReceiveAsync(date, id, vendorId, lines, Branch.CentralWarehouseId, invoiceAge: 5);
            }
        }

        private async Task ReceiveAsync(DateOnly date, Guid orderId, Guid vendorId, PurchaseOrderLineRequest[] lines, Guid warehouseId, int invoiceAge)
        {
            string prefix = "CPI";

            if (vendorId == _s._vendorMedion)
            {
                prefix = "MDN";
            }
            else if (vendorId == _s._vendorJapfa)
            {
                prefix = "JPF";
            }

            CreateGoodsReceiptResponse receipt = await _s.SendAsync(new CreateGoodsReceiptCommand(
                orderId, warehouseId, date, Invariant($"SJ-{prefix}-{date:yyMMdd}-{_s._vendorInvoiceSequence + 101}"), null,
                [.. lines.Select((l, i) => new GoodsReceiptLineRequest(i + 1, l.Quantity))]));

            _receipts.Add(new ReceivedGoods(
                receipt.Id, vendorId, prefix, [.. lines.Select(l => (l.Quantity, l.UnitPrice))], lines.Any(l => l.TaxCodeId == _s._ppn), invoiceAge));
        }

        private async Task ChickInAsync(DateOnly date)
        {
            (Guid docOrderId, Guid docVendorId, PurchaseOrderLineRequest[] docLines) = _orders[0];
            await ReceiveAsync(date, docOrderId, docVendorId, docLines, _coop.WarehouseId, invoiceAge: 3);

            await _s.SendAsync(new StartCycleCommand(_cycleId, date, [new ChickInLine(_s._doc, _spec.Population)]));
            _population = _spec.Population;

            await TransferAsync(date,
            [
                (_s._starterFeed, FeedSacks(1, 14)),
                (_s._ndibVaccine, Vials * 2 + 1),
                (_s._gumboroVaccine, Vials),
                (_s._vitamin, VitaminBottles * 5 + 1)
            ], "Kirim pakan starter & OVK saat chick-in");
        }

        private Task<CreateStockTransferResponse> TransferAsync(DateOnly date, (Guid ItemId, decimal Quantity)[] lines, string notes) =>
            _s.SendAsync(new CreateStockTransferCommand(
                Branch.CentralWarehouseId, _coop.WarehouseId, date, notes,
                [.. lines.Select(l => new StockTransferLineRequest(l.ItemId, UomOf(l.ItemId), l.Quantity))]));

        /// <summary>
        /// Feed is moved in sacks, OVK in its base unit.
        /// </summary>
        private Guid UomOf(Guid itemId) =>
            itemId == _s._starterFeed || itemId == _s._finisherFeed ? _s._uoms["SAK"] : _s._baseUoms[itemId];

        private async Task InvoiceVendorAsync(DateOnly date, ReceivedGoods goods)
        {
            int sequence = ++_s._vendorInvoiceSequence;

            Guid invoiceId = await _s.SendAsync(new CreateVendorInvoiceCommand(
                Branch.Id, goods.VendorId, Invariant($"{goods.VendorPrefix}/INV/{date:yyMM}/{sequence:D4}"),
                goods.Taxable ? Invariant($"04002{date:yy}{sequence:D8}") : null, date, null, null,
                [.. goods.Lines.Select((l, i) => new VendorInvoiceLineRequest(goods.GoodsReceiptId, i + 1, l.Quantity, l.UnitPrice))]));

            await _s.SendAsync(new PostVendorInvoiceCommand(invoiceId, null));

            _vendorInvoices.Add((invoiceId, goods.VendorId));
        }

        /// <summary>
        /// One payment voucher per vendor: made by the admin, approved by the checker, paid by the admin.
        /// </summary>
        private async Task PayVendorsAsync(DateOnly date)
        {
            foreach (IGrouping<Guid, (Guid Id, Guid VendorId)> vendor in _vendorInvoices.GroupBy(v => v.VendorId))
            {
                Guid[] ids = [.. vendor.Select(v => v.Id)];
                List<VendorInvoice> invoices = await _s.QueryDbAsync(db => db.VendorInvoices.Where(v => ids.Contains(v.Id)).ToListAsync(_s._ct));

                PaymentAllocationRequest[] allocations =
                    [.. invoices.Where(i => !i.Outstanding.IsZero).Select(i => new PaymentAllocationRequest(i.Id, i.Outstanding.Amount))];

                if (allocations.Length == 0)
                {
                    continue;
                }

                CreatePaymentVoucherResponse voucher = await _s.SendAsync(new CreatePaymentVoucherCommand(
                    Branch.BankId, vendor.Key, date, "Transfer BCA/BRI", "Pembayaran sapronak", allocations));
                await _s.SendAsAsync(_s._checkerId, new ApprovePaymentVoucherCommand(voucher.Id));
                await _s.SendAsync(new PayPaymentVoucherCommand(voucher.Id, date));
            }
        }

        // ---- Production ----------------------------------------------------------------------------------------------

        private decimal FeedGramPerBird(int age) => (13m + 4.3m * age) * _spec.FeedFactor;

        private decimal BodyWeightGram(int age) => (42m + 1.62m * age * age) * _spec.GrowthFactor;

        /// <summary>
        /// Sacks (50 kg) needed over the given ages, plus 2%: the initial population (an upper bound, depletion only
        /// lowers it) shrinking with every harvest truck, recorded before the truck leaves.
        /// </summary>
        private int FeedSacks(int fromAge, int toAge)
        {
            decimal kg = Enumerable.Range(fromAge, toAge - fromAge + 1).Sum(a =>
                _spec.Population * HarvestRemaining(a) * FeedGramPerBird(a) / 1000m);

            return (int)Math.Ceiling(kg * 1.02m / 50m);
        }

        private decimal HarvestRemaining(int age) =>
            (decimal)_spec.HarvestAges.Count(h => h >= age) / _spec.HarvestAges.Length;

        private decimal NextRandom()
        {
            _random = unchecked(_random * 1_664_525u + 1_013_904_223u);

            return (_random >> 8) / 16_777_216m;
        }

        private async Task RecordDayAsync(DateOnly date, int age)
        {
            decimal rate = age switch
            {
                <= 7 => 0.0030m,
                <= 21 => 0.0007m,
                _ => 0.0010m
            };
            int mortality = (int)Math.Round(_population * rate * _spec.MortalityFactor * (0.5m + NextRandom()));
            int culling = age >= 8 && NextRandom() < 0.2m ? 1 + (int)(NextRandom() * 4) : 0;
            decimal bodyWeight = Math.Round(BodyWeightGram(age) * (0.985m + NextRandom() * 0.03m), 0);

            Guid feed = age <= 14 ? _s._starterFeed : _s._finisherFeed;
            List<UsageRequest> usages = [new(feed, _s._uoms["KG"], Math.Round(_population * FeedGramPerBird(age) / 1000m, 1))];

            if (age is 4 or 18)
            {
                usages.Add(new UsageRequest(_s._ndibVaccine, _s._uoms["VIAL"], Vials));
            }

            if (age == 12)
            {
                usages.Add(new UsageRequest(_s._gumboroVaccine, _s._uoms["VIAL"], Vials));
            }

            if (age <= 10 && age % 2 == 0)
            {
                usages.Add(new UsageRequest(_s._vitamin, _s._uoms["BTL"], VitaminBottles));
            }

            string? notes = age switch
            {
                4 => "Vaksin ND-IB (tetes mata)",
                12 => "Vaksin Gumboro (air minum)",
                18 => "Booster ND-IB",
                _ => null
            };

            var command = new CreateDailyRecordingCommand(
                Guid.CreateVersion7(), _cycleId, date, mortality, culling, bodyWeight, notes, usages);
            Guid recordingId = await _s.SendAsync(command);

            _population -= mortality + culling;

            if (age == 10 && _spec.ReviseRecording)
            {
                _recordingToRevise = recordingId;
                _revisedRecording = command;
            }
        }

        private async Task ReviseRecordingAsync()
        {
            CreateDailyRecordingCommand original = _revisedRecording!;
            _revisedRecording = null;

            await _s.SendAsync(new ReviseDailyRecordingCommand(
                _recordingToRevise, "Koreksi PPL: 3 ekor mati di sudut kandang belum tercatat",
                original.Mortality + 3, original.Culling, original.AverageBodyWeightGram, original.Notes, original.Usages));

            _population -= 3;
        }

        private async Task HarvestAsync(DateOnly date, int age)
        {
            int trucksLeft = _spec.HarvestAges.Count(a => a >= age);
            int birds = trucksLeft == 1 ? _population : _population / trucksLeft;
            decimal weightKg = Math.Round(birds * BodyWeightGram(age) * (0.99m + NextRandom() * 0.02m) / 1000m, 1);
            int truck = ++_s._truckSequence;
            string vehicle = Invariant($"{Branch.Plate} {8100 + truck} {(char)('A' + truck % 26)}{(char)('K' + truck % 10)}");

            Guid harvestId = await _s.SendAsync(new RecordHarvestCommand(
                _cycleId, date, birds, weightKg, Invariant($"Truk {vehicle}, timbang di kandang")));
            _population -= birds;

            CreateDeliveryOrderResponse delivery = await _s.SendAsync(new CreateDeliveryOrderCommand(
                _salesOrderId, date, vehicle, Drivers[truck % Drivers.Length], null, [new DeliveryOrderLineRequest(1, harvestId)]));
            _deliveries[age] = delivery.Id;

            if (age == _lastHarvestAge)
            {
                await _s.SendAsync(new CloseSalesOrderCommand(_salesOrderId));
            }
        }

        // ---- Sales & receivables ---------------------------------------------------------------------------------

        private async Task OrderSalesAsync(DateOnly date)
        {
            decimal estimatedKg = Math.Round(_spec.Population * BodyWeightGram(_firstHarvestAge) / 1000m, 0);

            CreateSalesOrderResponse order = await _s.SendAsync(new CreateSalesOrderCommand(
                Branch.Id, _spec.CustomerId, date, _chickIn.AddDays(_firstHarvestAge), $"Panen {_spec.CoopCode}",
                [new SalesOrderLineRequest(_s._liveBird, _spec.Population, estimatedKg, _spec.PricePerKg, _s._ppnExempt)]));
            await _s.SendAsAsync(_s._checkerId, new ApproveSalesOrderCommand(order.Id, null));
            _salesOrderId = order.Id;

            if (_spec.AdvanceAmount > 0)
            {
                CreateCustomerReceiptResponse advance = await _s.SendAsync(new CreateCustomerReceiptCommand(
                    Branch.BankId, _spec.CustomerId, date, "TRF uang muka", $"Uang muka panen {_spec.CoopCode}", [], _spec.AdvanceAmount));
                _advanceReceiptId = advance.Id;
            }
        }

        private async Task RunReceivablesAsync(DateOnly date, int age)
        {
            if (_deliveries.TryGetValue(age - 1, out Guid deliveryId))
            {
                Guid invoiceId = await _s.SendAsync(new CreateSalesInvoiceCommand([deliveryId], date, null));
                await _s.SendAsync(new PostSalesInvoiceCommand(invoiceId));
                _invoices[age] = invoiceId;

                if (_advanceReceiptId != Guid.Empty && _invoices.Count == 1)
                {
                    decimal outstanding = await OutstandingAsync(invoiceId);
                    await _s.SendAsync(new ApplyCustomerAdvanceCommand(
                        _advanceReceiptId, date, [new ReceiptAllocationRequest(invoiceId, Math.Min(outstanding, _spec.AdvanceAmount))]));
                }
            }

            if (_spec.CreditNote && age == _firstHarvestAge + 3)
            {
                await _s.SendAsync(new CreateSalesCreditNoteCommand(
                    _invoices[_firstHarvestAge + 1], date, "Klaim susut timbang di RPA", [new CreditNoteLineRequest(1, 750_000m)]));
            }

            if (_spec.Receipts != ReceiptPlan.None && _invoices.TryGetValue(age - ReceiptLagDays, out Guid paidInvoiceId))
            {
                decimal outstanding = await OutstandingAsync(paidInvoiceId);
                decimal amount = _spec.Receipts == ReceiptPlan.Full ? outstanding : Math.Round(outstanding * 0.6m / 1000m) * 1000m;

                if (amount > 0)
                {
                    await _s.SendAsync(new CreateCustomerReceiptCommand(
                        Branch.BankId, _spec.CustomerId, date, "TRF pelunasan", null, [new ReceiptAllocationRequest(paidInvoiceId, amount)]));
                }
            }
        }

        private async Task<decimal> OutstandingAsync(Guid salesInvoiceId)
        {
            SalesInvoice invoice = await _s.QueryDbAsync(db => db.SalesInvoices.SingleAsync(i => i.Id == salesInvoiceId, _s._ct));

            return invoice.Outstanding.Amount;
        }

        // ---- Close & settlement ---------------------------------------------------------------------------------

        /// <summary>
        /// Leftover sapronak goes back to the central warehouse (or, for feed, on to another coop), then the cycle closes.
        /// </summary>
        private async Task CloseAsync(DateOnly date)
        {
            var leftovers = await _s.QueryDbAsync(db => db.StockBalances
                .Where(b => b.WarehouseId == _coop.WarehouseId && b.Quantity > 0)
                .Select(b => new { b.ItemId, b.Quantity })
                .ToListAsync(_s._ct));

            bool IsFeed(Guid itemId) => itemId == _s._starterFeed || itemId == _s._finisherFeed;

            var mutated = leftovers.Where(l => _spec.MoveLeftoverFeedTo is not null && IsFeed(l.ItemId)).ToList();
            var returned = leftovers.Except(mutated).ToList();

            if (mutated.Count > 0)
            {
                await _s.SendAsync(new CreateFeedMutationCommand(
                    _coop.WarehouseId, Branch.CentralWarehouseId, _s._coops[_spec.MoveLeftoverFeedTo!].WarehouseId, date,
                    "Sisa pakan dipindah ke kandang yang masih berjalan",
                    [.. mutated.Select(l => new StockTransferLineRequest(l.ItemId, _s._baseUoms[l.ItemId], l.Quantity))]));
            }

            if (returned.Count > 0)
            {
                await _s.SendAsync(new CreateStockReturnCommand(
                    _coop.WarehouseId, Branch.CentralWarehouseId, date, "Sisa sapronak akhir siklus", null,
                    [.. returned.Select(l => new StockTransferLineRequest(l.ItemId, _s._baseUoms[l.ItemId], l.Quantity))]));
            }

            await _s.SendAsync(new CloseCycleCommand(_cycleId));
        }

        private async Task SettleAsync(DateOnly date)
        {
            CreatePlasmaSettlementResponse settlement = await _s.SendAsync(new CreatePlasmaSettlementCommand(
                _cycleId, date, 0m, $"Settlement {_spec.CoopCode}"));
            _settlementId = settlement.Id;

            if (_spec.Settlement >= SettlementPlan.Approved)
            {
                await _s.SendAsAsync(_s._checkerId, new ApprovePlasmaSettlementCommand(settlement.Id));
            }
        }

        private async Task PayPlasmaAsync(DateOnly date)
        {
            PlasmaSettlement settlement = await _s.QueryDbAsync(db => db.PlasmaSettlements.SingleAsync(p => p.Id == _settlementId, _s._ct));

            if (settlement.Outstanding.IsZero || settlement.Outstanding.IsNegative)
            {
                return;
            }

            CreatePaymentVoucherResponse voucher = await _s.SendAsync(new CreatePlasmaPaymentVoucherCommand(
                Branch.BankId, _coop.FarmerId, date, "Transfer ke rekening plasma", $"Hasil siklus {_spec.CoopCode}",
                [new SettlementPaymentRequest(_settlementId, settlement.Outstanding.Amount)]));
            await _s.SendAsAsync(_s._checkerId, new ApprovePaymentVoucherCommand(voucher.Id));

            if (_spec.Settlement == SettlementPlan.Paid)
            {
                await _s.SendAsync(new PayPaymentVoucherCommand(voucher.Id, date));
            }
        }
    }
}
