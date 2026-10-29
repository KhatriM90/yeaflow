namespace YeaFlow.Sap.Contracts.Documents;

/// <summary>
/// User-defined fields YeaFlow needs on SAP marketing documents and payments.
/// VERIFY against $metadata: the UDF must be created on OINV/ORDR/… (alphanumeric, 50) during tenant onboarding.
/// </summary>
public static class SapUdf
{
    /// <summary>Holds the app transaction ID so an outcome-unknown post can be found again.</summary>
    public const string TransactionId = "U_YF_TxnId";
}
