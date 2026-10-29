namespace YeaFlow.Sap.Contracts.ServiceLayer;

/// <summary>Body of <c>POST /b1s/v2/Login</c>. Never sent to or from the browser.</summary>
public sealed record LoginRequest(string CompanyDB, string UserName, string Password)
{
    public override string ToString() => $"LoginRequest {{ CompanyDB = {CompanyDB}, UserName = {UserName}, Password = *** }}";
}

/// <summary>Response of <c>POST /b1s/v2/Login</c>; the session also arrives as the <c>B1SESSION</c> cookie.</summary>
public sealed record LoginResponse(string SessionId, string? Version, int SessionTimeout);
