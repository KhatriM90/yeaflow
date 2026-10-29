using Microsoft.Extensions.Logging.Abstractions;
using YeaFlow.Fiscal;

namespace YeaFlow.UnitTests.Fiscal;

public class TimsAddOnAdapterTests
{
    private static readonly TimsAddOnFieldMap Map = new()
    {
        StatusField = "U_TIMS_Status",
        SignedValues = new HashSet<string> { "Signed" },
        FailedValues = new HashSet<string> { "Error" },
        ControlUnitInvoiceNumberField = "U_TIMS_CUIN",
        ControlUnitSerialNumberField = "U_TIMS_CUSN",
        QrCodeField = "U_TIMS_QR",
        ErrorField = "U_TIMS_Err",
        TriggerField = "U_TIMS_Send",
        TriggerValue = "Y",
    };

    private static readonly FiscalDocumentRef Invoice = new(Guid.NewGuid(), Guid.NewGuid(), FiscalDocumentType.ArInvoice, "Invoices", 5);

    private sealed class FakeReader(Dictionary<string, string?> values) : ISapFieldReader
    {
        public Dictionary<string, string>? Written { get; private set; }

        public Task<IReadOnlyDictionary<string, string?>> ReadAsync(Guid tenantId, Guid companyId, string sapObject, int docEntry, IReadOnlyCollection<string> fields, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string?>>(values);

        public Task WriteAsync(Guid tenantId, Guid companyId, string sapObject, int docEntry, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
        {
            Written = new Dictionary<string, string>(fields);
            return Task.CompletedTask;
        }
    }

    private static TimsAddOnAdapter Adapter(FakeReader reader) => new(reader, Map, NullLogger<TimsAddOnAdapter>.Instance);

    [Fact]
    public async Task Signed_WithAllControlUnitData()
    {
        var reader = new FakeReader(new() { ["U_TIMS_Status"] = "Signed", ["U_TIMS_CUIN"] = "0040123450000001", ["U_TIMS_CUSN"] = "KRAMW004202207000001", ["U_TIMS_QR"] = "https://itax.kra.go.ke/x" });
        var status = await Adapter(reader).GetStatusAsync(Invoice, CancellationToken.None);
        Assert.Equal(FiscalState.Signed, status.State);
        Assert.Equal("0040123450000001", status.Signature!.ControlUnitInvoiceNumber);
    }

    [Fact]
    public async Task SignedFlag_WithoutCuin_IsStillPending()
    {
        var reader = new FakeReader(new() { ["U_TIMS_Status"] = "Signed", ["U_TIMS_CUSN"] = "X", ["U_TIMS_QR"] = "Y" });
        var status = await Adapter(reader).GetStatusAsync(Invoice, CancellationToken.None);
        Assert.Equal(FiscalState.Pending, status.State);
    }

    [Fact]
    public async Task Failed_ReportsAddOnError()
    {
        var reader = new FakeReader(new() { ["U_TIMS_Status"] = "Error", ["U_TIMS_Err"] = "CU offline" });
        var status = await Adapter(reader).GetStatusAsync(Invoice, CancellationToken.None);
        Assert.Equal(FiscalState.Failed, status.State);
        Assert.Equal("CU offline", status.FailureReason);
    }

    [Fact]
    public async Task Request_SetsTriggerUdf()
    {
        var reader = new FakeReader([]);
        await Adapter(reader).RequestAsync(Invoice, CancellationToken.None);
        Assert.Equal("Y", reader.Written!["U_TIMS_Send"]);
    }

    [Fact]
    public async Task PrintGuard_BlocksUntilSigned()
    {
        var pending = new FiscalPrintGuard(Adapter(new FakeReader(new() { ["U_TIMS_Status"] = "Queued" })));
        await Assert.ThrowsAsync<FiscalisationRequiredException>(() => pending.EnsureSignedAsync(Invoice, CancellationToken.None));

        var signed = new FiscalPrintGuard(Adapter(new FakeReader(new() { ["U_TIMS_Status"] = "Signed", ["U_TIMS_CUIN"] = "1", ["U_TIMS_CUSN"] = "2", ["U_TIMS_QR"] = "3" })));
        var signature = await signed.EnsureSignedAsync(Invoice, CancellationToken.None);
        Assert.Equal("1", signature.ControlUnitInvoiceNumber);
    }
}
