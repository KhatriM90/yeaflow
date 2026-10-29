namespace YeaFlow.Sap.Contracts.Documents;

/// <summary>Identifies a committed SAP document.</summary>
public sealed record SapDocumentRef(string SapObject, int DocEntry, int DocNum)
{
    public override string ToString() => $"{SapObject}/{DocEntry}";
}
