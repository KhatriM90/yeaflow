using System.Globalization;
using Microsoft.Extensions.Logging;

namespace YeaFlow.Fiscal;

/// <summary>
/// Works with a tenant's existing SAP B1 TIMS add-on or middleware: optionally sets its trigger flag,
/// then reads signed status and control-unit data back from the document's UDFs. Never signs anything itself.
/// </summary>
public sealed class TimsAddOnAdapter : IFiscalisationAdapter
{
    private readonly ISapFieldReader _reader;
    private readonly TimsAddOnFieldMap _map;
    private readonly ILogger<TimsAddOnAdapter> _logger;

    public TimsAddOnAdapter(ISapFieldReader reader, TimsAddOnFieldMap map, ILogger<TimsAddOnAdapter> logger)
    {
        _reader = reader;
        _map = map;
        _logger = logger;
    }

    public string Name => "tims-addon";

    public async Task RequestAsync(FiscalDocumentRef document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(_map.TriggerField) || _map.TriggerValue is null)
        {
            return;
        }

        // Writes a UDF only, through Service Layer (PATCH), never SQL.
        await _reader.WriteAsync(
            document.TenantId,
            document.CompanyId,
            document.SapObject,
            document.DocEntry,
            new Dictionary<string, string> { [_map.TriggerField] = _map.TriggerValue },
            cancellationToken);
    }

    public async Task<FiscalStatus> GetStatusAsync(FiscalDocumentRef document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var values = await _reader.ReadAsync(
            document.TenantId, document.CompanyId, document.SapObject, document.DocEntry, _map.AllReadFields(), cancellationToken);

        var status = Get(values, _map.StatusField);
        if (status is not null && _map.FailedValues.Contains(status))
        {
            return FiscalStatus.FailedWith(Get(values, _map.ErrorField) ?? $"TIMS integration reported '{status}'.");
        }

        if (status is null || !_map.SignedValues.Contains(status))
        {
            return FiscalStatus.Pending;
        }

        var cuin = Get(values, _map.ControlUnitInvoiceNumberField);
        var serial = Get(values, _map.ControlUnitSerialNumberField);
        var qr = Get(values, _map.QrCodeField);
        if (cuin is null || serial is null || qr is null)
        {
            // Signed flag without the data we must print is not good enough for a final tax invoice.
            _logger.LogWarning("TIMS status signed but control-unit data incomplete for {SapObject}/{DocEntry}", document.SapObject, document.DocEntry);
            return FiscalStatus.Pending;
        }

        DateTimeOffset? signedAt = DateTimeOffset.TryParse(Get(values, _map.SignedAtField), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

        return FiscalStatus.SignedWith(new FiscalSignature(
            cuin, serial, qr, Get(values, _map.SellerPinField), Get(values, _map.BuyerPinField), signedAt));
    }

    private static string? Get(IReadOnlyDictionary<string, string?> values, string? field) =>
        field is not null && values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
