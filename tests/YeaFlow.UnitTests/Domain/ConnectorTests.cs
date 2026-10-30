using YeaFlow.Application.Connectors;
using YeaFlow.Domain.Common;
using YeaFlow.Domain.Connectors;
using ConnectorEntity = YeaFlow.Domain.Connectors.Connector;

namespace YeaFlow.UnitTests.Domain;

// D-007: connector registration, token handling and the relay command lifecycle.
public sealed class ConnectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.NewGuid();

    private static ConnectorCommand NewCommand(ConnectorCommandKind kind = ConnectorCommandKind.PostDocument) =>
        new(Tenant, Guid.NewGuid(), Guid.NewGuid(), "SBO_ACME", kind, "Orders", "TX-1", kind == ConnectorCommandKind.PostDocument ? "{}" : null, Now);

    [Fact]
    public void Register_StoresOnlyTheHash_AndVerifiesTheSecret()
    {
        var (connector, secret) = ConnectorEntity.Register(Tenant, "Head office", Now);

        Assert.DoesNotContain(secret, connector.SecretHash, StringComparison.Ordinal);
        Assert.True(connector.Verify(secret));
        Assert.False(connector.Verify(secret + "x"));
        Assert.False(connector.Verify(string.Empty));
    }

    [Fact]
    public void DisabledConnector_DoesNotVerify()
    {
        var (connector, secret) = ConnectorEntity.Register(Tenant, "Head office", Now);
        connector.Disable();
        Assert.False(connector.Verify(secret));
    }

    [Fact]
    public void IsOnline_FollowsLastContact()
    {
        var (connector, _) = ConnectorEntity.Register(Tenant, "Head office", Now);
        Assert.False(connector.IsOnline(Now, TimeSpan.FromMinutes(2)));

        connector.RecordContact("1.0.0", Now);
        Assert.True(connector.IsOnline(Now.AddMinutes(1), TimeSpan.FromMinutes(2)));
        Assert.False(connector.IsOnline(Now.AddMinutes(3), TimeSpan.FromMinutes(2)));
        Assert.Equal("1.0.0", connector.Version);
    }

    [Fact]
    public void Token_RoundTrips()
    {
        var connectorId = Guid.NewGuid();
        var token = ConnectorToken.Format(Tenant, connectorId, "s3cret-value-long-enough-0123");

        Assert.True(ConnectorToken.TryParse(token, out var tenantId, out var parsedConnector, out var secret));
        Assert.Equal(Tenant, tenantId);
        Assert.Equal(connectorId, parsedConnector);
        Assert.Equal("s3cret-value-long-enough-0123", secret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("abc.def.ghi")]
    [InlineData("0123456789abcdef0123456789abcdef.0123456789abcdef0123456789abcdef.short")]
    public void Token_RejectsMalformedValues(string value) =>
        Assert.False(ConnectorToken.TryParse(value, out _, out _, out _));

    [Fact]
    public void Command_Lease_ThenComplete()
    {
        var command = NewCommand();
        command.Lease(Now);
        Assert.Equal(ConnectorCommandState.Leased, command.State);

        command.Complete(ConnectorOutcome.Posted, 42, 1042, null, null, Now.AddSeconds(1));
        Assert.Equal(ConnectorCommandState.Completed, command.State);
        Assert.Equal(42, command.SapDocEntry);
        Assert.Equal(1042, command.SapDocNum);
    }

    [Fact]
    public void Command_CanBeWithdrawn_OnlyBeforeLease()
    {
        var pending = NewCommand();
        pending.Withdraw(Now);
        Assert.Equal(ConnectorCommandState.Withdrawn, pending.State);

        var leased = NewCommand();
        leased.Lease(Now);
        Assert.Throws<DomainException>(() => leased.Withdraw(Now));
    }

    [Fact]
    public void WithdrawnCommand_CannotBeLeasedOrCompleted()
    {
        var command = NewCommand();
        command.Withdraw(Now);
        Assert.Throws<DomainException>(() => command.Lease(Now));
        Assert.Throws<DomainException>(() => command.Complete(ConnectorOutcome.Posted, 1, 1, null, null, Now));
    }

    [Fact]
    public void Posted_RequiresDocumentReference()
    {
        var command = NewCommand();
        command.Lease(Now);
        Assert.Throws<DomainException>(() => command.Complete(ConnectorOutcome.Posted, null, null, null, null, Now));
    }

    [Fact]
    public void LookupOutcome_IsInvalidForPostCommand()
    {
        var command = NewCommand();
        command.Lease(Now);
        Assert.Throws<DomainException>(() => command.Complete(ConnectorOutcome.Absent, null, null, null, null, Now));
    }

    [Fact]
    public void Find_AcceptsAbsent()
    {
        var command = NewCommand(ConnectorCommandKind.FindByTransactionId);
        command.Lease(Now);
        command.Complete(ConnectorOutcome.Absent, null, null, null, null, Now);
        Assert.Equal(ConnectorOutcome.Absent, command.Outcome);
    }
}
