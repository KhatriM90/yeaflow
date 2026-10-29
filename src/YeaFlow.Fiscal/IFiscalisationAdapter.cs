namespace YeaFlow.Fiscal;

/// <summary>
/// Pluggable fiscalisation step (decision D-003). The baseline adapter observes each tenant's existing SAP B1 TIMS
/// integration; native TIMS and KRA eTIMS (OSCU/VSCU) adapters plug in here later without changing document flows.
/// </summary>
public interface IFiscalisationAdapter
{
    /// <summary>Adapter key stored in tenant configuration, e.g. <c>tims-addon</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Asks the integration to fiscalise a newly posted document, where the integration needs a trigger.
    /// Add-ons that fiscalise on posting treat this as a no-op.
    /// </summary>
    Task RequestAsync(FiscalDocumentRef document, CancellationToken cancellationToken);

    Task<FiscalStatus> GetStatusAsync(FiscalDocumentRef document, CancellationToken cancellationToken);
}
