using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using YeaFlow.Application.Common;
using YeaFlow.Application.Postings;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Posting;
using YeaFlow.Sap.Contracts.Documents;

namespace YeaFlow.UnitTests.Application;

public class PostingServiceTests
{
    private const string Payload = """{"CardCode":"C001","DocumentLines":[{"ItemCode":"A1","Quantity":2}]}""";

    private readonly Guid _company = Guid.NewGuid();
    private readonly InMemoryIntents _intents = new();
    private readonly RecordingAudit _audit = new();
    private readonly ScriptedSapGateway _sap = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));
    private readonly RetryPolicy _policy = new() { SettleWindow = TimeSpan.FromMinutes(5) };

    private PostingService Service(IExecutionContext? context = null) => new(
        _intents, _sap, _intents, _audit, context ?? SalesUser(), _time, _policy, NullLogger<PostingService>.Instance);

    private FakeContext SalesUser() => new()
    {
        Permissions = RolePermissions.For(Role.Sales),
        CompanyIds = new HashSet<Guid> { _company },
    };

    private CreatePostingCommand Order(string txn = "TX-1", string payload = Payload) =>
        new(_company, txn, SapObjects.Orders, payload, SubmitNow: true);

    [Fact]
    public async Task Create_PersistsIntentBeforeAnySapCall()
    {
        var intent = await Service().CreateAsync(Order(), CancellationToken.None);

        Assert.Equal(PostingState.Queued, intent.State);
        Assert.Single(_intents.Items);
        Assert.Equal(0, _sap.PostCalls);
        Assert.Contains(_audit.Events, e => e.Action == "posting.created");
    }

    [Fact]
    public async Task DuplicateClick_ReturnsSameIntent()
    {
        var service = Service();
        var first = await service.CreateAsync(Order(), CancellationToken.None);
        var second = await service.CreateAsync(Order(), CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(_intents.Items);
    }

    [Fact]
    public async Task ReusedTransactionId_WithDifferentPayload_IsConflict()
    {
        var service = Service();
        await service.CreateAsync(Order(), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Order(payload: """{"CardCode":"C999"}"""), CancellationToken.None));
        Assert.Equal("posting.transaction_id_reused", ex.Code);
    }

    [Fact]
    public async Task UserWithoutPermission_IsForbidden()
    {
        var warehouse = new FakeContext { Permissions = RolePermissions.For(Role.Warehouse), CompanyIds = new HashSet<Guid> { _company } };
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(warehouse).CreateAsync(Order(), CancellationToken.None));
        Assert.Empty(_intents.Items);
    }

    [Fact]
    public async Task UserWithoutCompanyAccess_IsForbidden()
    {
        var otherCompany = new FakeContext { Permissions = RolePermissions.For(Role.Sales), CompanyIds = new HashSet<Guid> { Guid.NewGuid() } };
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(otherCompany).CreateAsync(Order(), CancellationToken.None));
    }

    [Fact]
    public async Task UnsupportedSapObject_FailsValidation()
    {
        var command = new CreatePostingCommand(_company, "TX-1", "JournalEntries", Payload, true);
        await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Process_Posted_StoresSapReference()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order(), CancellationToken.None);
        _sap.PostOutcomes.Enqueue(new SapPostOutcome.Posted(new SapDocumentRef(SapObjects.Orders, 10, 1010)));

        await service.ProcessAsync(intent.Id, CancellationToken.None);

        Assert.Equal(PostingState.Posted, intent.State);
        Assert.Equal(1010, intent.SapDocNum);
        Assert.Contains(_audit.Events, e => e is { Action: "posting.submitted", Result: "posted" });
    }

    [Fact]
    public async Task Process_SapRejection_FailsWithSapMessage()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order(), CancellationToken.None);
        _sap.PostOutcomes.Enqueue(new SapPostOutcome.Rejected("-10", "Period is locked"));

        await service.ProcessAsync(intent.Id, CancellationToken.None);

        Assert.Equal(PostingState.Failed, intent.State);
        Assert.Equal("Period is locked", intent.LastErrorMessage);
    }

    [Fact]
    public async Task Process_TimeoutAfterSend_IsOutcomeUnknown_ThenReconciledWithoutRepost()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order(), CancellationToken.None);
        _sap.PostOutcomes.Enqueue(new SapPostOutcome.Unknown("timeout"));
        await service.ProcessAsync(intent.Id, CancellationToken.None);
        Assert.Equal(PostingState.OutcomeUnknown, intent.State);

        _sap.LookupOutcomes.Enqueue(new SapLookupOutcome.Found(new SapDocumentRef(SapObjects.Orders, 11, 1011)));
        await Service(new FakeContext { TenantId = intent.TenantId, IsSystem = true, UserId = null }).ReconcileAsync(intent.Id, CancellationToken.None);

        Assert.Equal(PostingState.Posted, intent.State);
        Assert.Equal(1, _sap.PostCalls);
    }

    [Fact]
    public async Task Process_UnexpectedException_IsTreatedAsOutcomeUnknown()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order(), CancellationToken.None);
        _sap.ThrowOnPost = new InvalidOperationException("relay crashed");

        await service.ProcessAsync(intent.Id, CancellationToken.None);

        Assert.Equal(PostingState.OutcomeUnknown, intent.State);
    }

    [Fact]
    public async Task Process_IgnoresIntentNotQueued()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order() with { SubmitNow = false }, CancellationToken.None);
        await service.ProcessAsync(intent.Id, CancellationToken.None);
        Assert.Equal(PostingState.Draft, intent.State);
        Assert.Equal(0, _sap.PostCalls);
    }

    [Fact]
    public async Task Reconcile_RequiresPermission()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order(), CancellationToken.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReconcileAsync(intent.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Reconcile_SapUnavailable_ChangesNothing()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order(), CancellationToken.None);
        _sap.PostOutcomes.Enqueue(new SapPostOutcome.Unknown("timeout"));
        await service.ProcessAsync(intent.Id, CancellationToken.None);

        _sap.LookupOutcomes.Enqueue(new SapLookupOutcome.Unavailable("connector offline"));
        var admin = new FakeContext { TenantId = intent.TenantId, Permissions = RolePermissions.For(Role.AppAdministrator), CompanyIds = new HashSet<Guid> { _company } };
        await Service(admin).ReconcileAsync(intent.Id, CancellationToken.None);

        Assert.Equal(PostingState.OutcomeUnknown, intent.State);
        Assert.Equal(0, intent.ReconcileAttempts);
    }

    [Fact]
    public async Task Jobs_MarkStuckSubmissionsUnknown_AndReconcile()
    {
        var service = Service();
        var intent = await service.CreateAsync(Order(), CancellationToken.None);
        intent.BeginSubmission(_time.GetUtcNow());   // worker died here
        _time.Advance(TimeSpan.FromMinutes(6));

        _sap.LookupOutcomes.Enqueue(new SapLookupOutcome.Absent());
        var system = new FakeContext { TenantId = intent.TenantId, IsSystem = true, UserId = null };
        var jobs = new PostingJobs(_intents, Service(system), _intents, _time, _policy, NullLogger<PostingJobs>.Instance);
        await jobs.ReconcileUnknownAsync(CancellationToken.None);

        // Absent after the settle window with retries left: safe to queue again.
        Assert.Equal(PostingState.Queued, intent.State);
    }
}
