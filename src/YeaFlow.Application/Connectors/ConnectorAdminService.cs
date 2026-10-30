using FluentValidation;
using YeaFlow.Application.Common;
using YeaFlow.Application.Tenancy;
using YeaFlow.Domain.Connectors;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Application.Connectors;

public interface IConnectorRepository
{
    Task<Connector?> GetAsync(Guid connectorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Connector>> ListAsync(CancellationToken cancellationToken);

    void Add(Connector connector);
}

public interface ICompanyRepository
{
    Task<Company?> GetAsync(Guid companyId, CancellationToken cancellationToken);

    Task<bool> SapCompanyDbExistsAsync(string sapCompanyDb, CancellationToken cancellationToken);

    void Add(Company company);
}

public sealed record AddCompanyCommand(string Name, string SapCompanyDb, SapDatabaseType DatabaseType, string LocalCurrency, Guid ConnectorId);

public sealed class AddCompanyCommandValidator : AbstractValidator<AddCompanyCommand>
{
    public AddCompanyCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SapCompanyDb).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_]+$");
        RuleFor(c => c.DatabaseType).IsInEnum();
        RuleFor(c => c.LocalCurrency).NotEmpty().Length(3);
        RuleFor(c => c.ConnectorId).NotEmpty();
    }
}

public sealed record ConnectorRegistration(Guid ConnectorId, string Token);

public sealed record ConnectorStatus(Guid Id, string Name, bool IsEnabled, string? Version, DateTimeOffset? LastSeenAt, bool Online);

/// <summary>
/// Operator console use cases for connectors and companies (scope section 8, multi-tenant platform operations).
/// Runs inside the target tenant's scope; the caller must already hold the operator permission.
/// </summary>
public sealed class ConnectorAdminService
{
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(2);

    private readonly IConnectorRepository _connectors;
    private readonly ICompanyRepository _companies;
    private readonly ITenantRepository _tenants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _audit;
    private readonly IExecutionContext _context;
    private readonly TimeProvider _time;

    public ConnectorAdminService(IConnectorRepository connectors, ICompanyRepository companies, ITenantRepository tenants, IUnitOfWork unitOfWork, IAuditLog audit, IExecutionContext context, TimeProvider time)
    {
        _connectors = connectors;
        _companies = companies;
        _tenants = tenants;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _context = context;
        _time = time;
    }

    /// <summary>Registers a connector and returns its token. The token is shown once and stored only as a hash.</summary>
    public async Task<ConnectorRegistration> RegisterConnectorAsync(string name, string requestedBy, CancellationToken cancellationToken)
    {
        Authorize.Require(_context, TenantOnboardingService.OperatorPermission);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _ = await _tenants.GetAsync(_context.TenantId, cancellationToken) ?? throw new NotFoundException("Tenant not found.");

        var (connector, secret) = Connector.Register(_context.TenantId, name, _time.GetUtcNow());
        _connectors.Add(connector);
        _audit.Record("connector.registered", "ok", detail: $"{connector.Name} by {requestedBy}");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ConnectorRegistration(connector.Id, ConnectorToken.Format(_context.TenantId, connector.Id, secret));
    }

    public async Task<Company> AddCompanyAsync(AddCompanyCommand command, string requestedBy, CancellationToken cancellationToken)
    {
        Authorize.Require(_context, TenantOnboardingService.OperatorPermission);
        ArgumentNullException.ThrowIfNull(command);
        await new AddCompanyCommandValidator().ValidateAndThrowAsync(command, cancellationToken);
        _ = await _connectors.GetAsync(command.ConnectorId, cancellationToken) ?? throw new NotFoundException("Connector not found.");
        if (await _companies.SapCompanyDbExistsAsync(command.SapCompanyDb, cancellationToken))
        {
            throw new ConflictException("company.exists", $"Company database {command.SapCompanyDb} is already registered.");
        }

        var company = new Company(Guid.NewGuid(), _context.TenantId, command.Name, command.SapCompanyDb, command.DatabaseType, command.LocalCurrency.ToUpperInvariant());
        company.AssignConnector(command.ConnectorId);
        _companies.Add(company);
        _audit.Record("company.added", "ok", company.Id, detail: $"{company.SapCompanyDb} by {requestedBy}");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return company;
    }

    public async Task<IReadOnlyList<ConnectorStatus>> ListConnectorsAsync(CancellationToken cancellationToken)
    {
        Authorize.Require(_context, TenantOnboardingService.OperatorPermission);
        var now = _time.GetUtcNow();
        return (await _connectors.ListAsync(cancellationToken))
            .Select(c => new ConnectorStatus(c.Id, c.Name, c.IsEnabled, c.Version, c.LastSeenAt, c.IsOnline(now, OnlineWindow)))
            .ToList();
    }
}

/// <summary>Connector token: <c>{tenantId:N}.{connectorId:N}.{secret}</c>. The tenant part lets the API apply row-level security before lookup.</summary>
public static class ConnectorToken
{
    public static string Format(Guid tenantId, Guid connectorId, string secret) => $"{tenantId:N}.{connectorId:N}.{secret}";

    public static bool TryParse(string? token, out Guid tenantId, out Guid connectorId, out string secret)
    {
        tenantId = Guid.Empty;
        connectorId = Guid.Empty;
        secret = string.Empty;
        var parts = token?.Split('.', 3);
        if (parts is not { Length: 3 } || !Guid.TryParseExact(parts[0], "N", out tenantId) || !Guid.TryParseExact(parts[1], "N", out connectorId) || parts[2].Length < 20)
        {
            return false;
        }

        secret = parts[2];
        return true;
    }
}
