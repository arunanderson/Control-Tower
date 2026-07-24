using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlTower.Host.Web.Tests;

public class ExperienceApiTests(LocalJwtWebFactory factory) : IClassFixture<LocalJwtWebFactory>
{
    private HttpClient TenantClient()
    {
        return factory.AuthenticatedClient(Guid.NewGuid());
    }

    [Theory]
    [InlineData("/api/portfolio/assets")]
    [InlineData("/api/economics/executive")]
    [InlineData("/api/governance/cases")]
    [InlineData("/api/trust/coverage")]
    [InlineData("/api/resolution/merge-cases")]
    public async Task Api_requires_a_tenant(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/portfolio/assets")]
    [InlineData("/api/economics/executive")]
    [InlineData("/api/economics/portfolio")]
    [InlineData("/api/economics/departments")]
    [InlineData("/api/economics/agents")]
    [InlineData("/api/governance/cases")]
    [InlineData("/api/governance/debt")]
    [InlineData("/api/trust/coverage")]
    [InlineData("/api/resolution/merge-cases")]
    public async Task Api_returns_200_within_a_tenant_scope(string path)
    {
        var response = await TenantClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Economics_executive_carries_evidence_fields()
    {
        var body = await TenantClient().GetStringAsync("/api/economics/executive");
        Assert.Contains("evidenceClass", body);
        Assert.Contains("methodology", body);
        Assert.Contains("asOf", body);
        Assert.Contains("validationState", body);
    }

    [Fact]
    public async Task Reporting_period_api_exposes_signed_freeze_and_immutable_restatement_history()
    {
        var tenant = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var client = ClientFor(tenant, objectId);
        client.DefaultRequestHeaders.Add("X-Operator", "forged@example.com");
        var canonicalActor = await ResolvedActorAsync(
            client,
            tenant,
            objectId);
        var create = await client.PostAsJsonAsync("/api/economics/reporting-periods", new
        {
            start = "2026-06-01T00:00:00Z",
            end = "2026-07-01T00:00:00Z",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var periodId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync())).RootElement
            .GetProperty("periodId").GetGuid();

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/economics/reporting-periods/{periodId}/closing", new { })).StatusCode);
        var basis = new
        {
            asOf = "2026-06-30T23:59:59Z",
            sourceReferences = new[] { "economics@2026-06-30" },
            ruleVersionReferences = new[] { "allocation:7" },
            organisationModelVersion = "org:12",
            observationWatermark = "observation:100",
        };
        var freeze = await client.PostAsJsonAsync($"/api/economics/reporting-periods/{periodId}/freeze", new
        {
            payloadJson = "{\"totalSpend\":125.50}",
            inputBasis = basis,
        });
        Assert.Equal(HttpStatusCode.OK, freeze.StatusCode);
        var firstBody = await freeze.Content.ReadAsStringAsync();
        Assert.Contains(canonicalActor, firstBody);
        Assert.DoesNotContain("forged@example.com", firstBody);

        var restate = await client.PostAsJsonAsync($"/api/economics/reporting-periods/{periodId}/restate", new
        {
            payloadJson = "{\"totalSpend\":127.00}",
            inputBasis = new
            {
                basis.asOf,
                basis.sourceReferences,
                basis.ruleVersionReferences,
                basis.organisationModelVersion,
                observationWatermark = "observation:104",
            },
            reason = "Late invoice",
        });
        Assert.Equal(HttpStatusCode.OK, restate.StatusCode);

        var history = await client.GetStringAsync($"/api/economics/reporting-periods/{periodId}/snapshots");
        using var document = JsonDocument.Parse(history);
        Assert.Equal(2, document.RootElement.GetArrayLength());
        Assert.Equal(1, document.RootElement[0].GetProperty("version").GetInt32());
        Assert.Equal("{\"totalSpend\":125.50}", document.RootElement[0].GetProperty("payloadJson").GetString());
        Assert.Equal(2, document.RootElement[1].GetProperty("version").GetInt32());
        Assert.Equal("Late invoice", document.RootElement[1].GetProperty("restatementReason").GetString());
    }

    [Fact]
    public async Task Reporting_period_commands_require_authentication()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/economics/reporting-periods", new
        {
            start = "2026-06-01T00:00:00Z",
            end = "2026-07-01T00:00:00Z",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Trust_coverage_is_reported_honestly()
    {
        var body = await TenantClient().GetStringAsync("/api/trust/coverage");
        Assert.Contains("providersConnected", body);
        Assert.Contains("coverageNote", body);
    }

    private HttpClient PrivilegedClient(
        Guid tenant,
        Guid? objectId = null,
        string purpose = "Support investigation")
    {
        var client = AdminClient(tenant, objectId);
        client.DefaultRequestHeaders.Add("X-Actor", "forged-actor");
        client.DefaultRequestHeaders.Add("X-Purpose", purpose);
        return client;
    }

    private sealed record AccessDto(
        Guid AccessId,
        string Actor,
        string Purpose,
        string Resource,
        DateTimeOffset OccurredAt,
        bool PolicyApplicable,
        string? PolicyVersion,
        string CorrelationId);

    [Fact]
    public async Task Privileged_log_requires_actor_and_purpose()
    {
        var response = await AdminClient(Guid.NewGuid())
            .GetAsync("/api/trust/privileged-access");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Privileged_log_read_is_recorded_immutably_and_visible_on_the_next_read()
    {
        var tenant = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var client = PrivilegedClient(tenant, objectId);
        var canonicalActor = await ResolvedActorAsync(
            client,
            tenant,
            objectId);
        var firstRead = await client.GetFromJsonAsync<
            List<AccessDto>>(
            "/api/trust/privileged-access");
        Assert.DoesNotContain(
            firstRead!,
            entry =>
                entry.Resource
                == "trust.privileged-access-log");

        var records = await client.GetFromJsonAsync<List<AccessDto>>("/api/trust/privileged-access");
        var record = Assert.Single(
            records!,
            entry =>
                entry.Resource
                == "trust.privileged-access-log");
        Assert.Equal(canonicalActor, record.Actor);
        Assert.NotEqual("forged-actor", record.Actor);
        Assert.Equal("Support investigation", record.Purpose);
        Assert.Equal("trust.privileged-access-log", record.Resource);
        Assert.NotEqual(Guid.Empty, record.AccessId);
        Assert.NotEqual(default, record.OccurredAt);
        Assert.Equal(TimeSpan.Zero, record.OccurredAt.Offset);
        Assert.Equal(
            0,
            record.OccurredAt.Ticks
            % TimeSpan.TicksPerMicrosecond);
        Assert.False(record.PolicyApplicable);
        Assert.Null(record.PolicyVersion);
        Assert.False(string.IsNullOrWhiteSpace(record.CorrelationId));
        var responseEvidence =
            JsonSerializer.Serialize(records);
        Assert.DoesNotContain(
            objectId.ToString("D"),
            responseEvidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            objectId.ToString("N"),
            responseEvidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "forged-actor",
            responseEvidence,
            StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var tenants = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var _ = tenants.BeginScope(new TenantId(tenant));
        var stream = await scope.ServiceProvider.GetRequiredService<IEventStore>().ReadAllAsync();
        Assert.Contains(stream, e => System.Text.Encoding.UTF8.GetString(e.Payload).Contains("trust.privileged-access-log"));
    }

    [Fact]
    public async Task L1_reads_are_not_misclassified_and_audit_log_is_tenant_isolated()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await ClientFor(tenantA).GetAsync("/api/trust/coverage");
        Assert.DoesNotContain(
            (await PrivilegedClient(tenantA)
                .GetFromJsonAsync<List<AccessDto>>(
                    "/api/trust/privileged-access"))!,
            entry =>
                entry.Resource
                == "trust.privileged-access-log");
        Assert.DoesNotContain(
            (await PrivilegedClient(tenantB)
                .GetFromJsonAsync<List<AccessDto>>(
                    "/api/trust/privileged-access"))!,
            entry =>
                entry.Resource
                == "trust.privileged-access-log");
    }

    [Fact]
    public void Development_host_composes_the_complete_privileged_evidence_path()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<PrivilegedReadEvidenceAuditor>(
            scope.ServiceProvider
                .GetRequiredService<
                    IPrivilegedReadAuditor>());
    }

    [Fact]
    public async Task Legal_hold_protects_only_matching_retention_subjects_and_is_tenant_isolated()
    {
        var tenantA = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var client = AdminClient(tenantA, objectId);
        client.DefaultRequestHeaders.Add("X-Operator", "forged@example.com");
        var canonicalActor = await ResolvedActorAsync(
            client,
            tenantA,
            objectId);
        var placed = await client.PostAsJsonAsync("/api/trust/legal-holds", new
        {
            dataClass = "UsageCostObservations",
            resourceReference = "asset:42",
            reason = "Regulatory inquiry",
        });
        Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        var holds = await client.GetFromJsonAsync<List<LegalHoldView>>("/api/trust/legal-holds");
        Assert.Equal(canonicalActor, Assert.Single(holds!).PlacedBy);

        using (var scope = factory.Services.CreateScope())
        {
            var tenants = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            using var _ = tenants.BeginScope(new TenantId(tenantA));
            var service = scope.ServiceProvider.GetRequiredService<LegalHoldService>();
            Assert.True(await service.IsProtectedAsync(
                new RetentionSubject(RetentionDataClass.UsageCostObservations, "asset:42")));
            Assert.False(await service.IsProtectedAsync(
                new RetentionSubject(RetentionDataClass.UsageCostObservations, "asset:43")));
            Assert.False(await service.IsProtectedAsync(
                new RetentionSubject(RetentionDataClass.InventoryObservations, "asset:42")));
        }

        var otherTenant = await AdminClient(Guid.NewGuid())
            .GetFromJsonAsync<List<LegalHoldView>>("/api/trust/legal-holds");
        Assert.Empty(otherTenant!);
    }

    [Fact]
    public async Task Legal_hold_release_requires_approval_and_preserves_audited_history()
    {
        var tenant = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var client = AdminClient(tenant, objectId);
        var canonicalActor = await ResolvedActorAsync(
            client,
            tenant,
            objectId);
        var placed = await client.PostAsJsonAsync("/api/trust/legal-holds", new
        {
            dataClass = "All",
            reason = "Litigation",
        });
        var holdId = (await JsonDocument.ParseAsync(await placed.Content.ReadAsStreamAsync())).RootElement
            .GetProperty("holdId").GetGuid();

        var missingApproval = await client.PostAsJsonAsync($"/api/trust/legal-holds/{holdId}/release", new { reason = "Matter closed" });
        Assert.Equal(HttpStatusCode.BadRequest, missingApproval.StatusCode);
        client.DefaultRequestHeaders.Add("X-Approval-Reference", "approval:GC-104");
        var released = await client.PostAsJsonAsync($"/api/trust/legal-holds/{holdId}/release", new { reason = "Matter closed" });
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);

        var history = await client.GetFromJsonAsync<List<LegalHoldView>>("/api/trust/legal-holds");
        var hold = Assert.Single(history!);
        Assert.False(hold.IsActive);
        Assert.Equal(canonicalActor, hold.PlacedBy);
        Assert.Equal(canonicalActor, hold.ReleasedBy);
        Assert.Equal("approval:GC-104", hold.ApprovalReference);
        Assert.Equal("Matter closed", hold.ReleaseReason);

        using var scope = factory.Services.CreateScope();
        var tenants = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var _ = tenants.BeginScope(new TenantId(tenant));
        var stream = await scope.ServiceProvider.GetRequiredService<IEventStore>().ReadAllAsync();
        Assert.Contains(stream, e => System.Text.Encoding.UTF8.GetString(e.Payload).Contains("Regulatory", StringComparison.Ordinal) ||
            System.Text.Encoding.UTF8.GetString(e.Payload).Contains("Litigation", StringComparison.Ordinal));
        Assert.Contains(stream, e => System.Text.Encoding.UTF8.GetString(e.Payload).Contains("approval:GC-104", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_legal_hold_evidence_never_changes_hold_state()
    {
        var tenant = new TenantId(Guid.NewGuid());
        using var scope = factory.Services.CreateScope();
        var tenants =
            scope.ServiceProvider
                .GetRequiredService<ITenantContextAccessor>();
        using var _ = tenants.BeginScope(tenant);
        var service =
            scope.ServiceProvider
                .GetRequiredService<LegalHoldService>();
        var events =
            scope.ServiceProvider
                .GetRequiredService<IEventStore>();
        var actor = AuditActor.System("legal-hold-test");

        foreach (var invalidReason in new[]
                 {
                     $" {new string('x', 16)} ",
                     new string('x', 2049),
                     "invalid\u0001reason",
                     "invalid\uD800reason",
                 })
        {
            await Assert.ThrowsAsync<LegalHoldException>(
                () => service.PlaceAsync(
                    new LegalHoldScope(
                        RetentionDataClass.DomainEvents),
                    invalidReason,
                    actor));
        }

        Assert.Empty(await service.ListAsync());
        Assert.Empty(await events.ReadAllAsync());

        var holdId = await service.PlaceAsync(
            new LegalHoldScope(
                RetentionDataClass.DomainEvents),
            "Regulatory preservation",
            actor);
        var baselineEventCount =
            (await events.ReadAllAsync()).Count;

        var invalidReleases = new[]
        {
            (Reason: $" {new string('x', 16)} ", Approval: "GC-100"),
            (Reason: new string('x', 2049), Approval: "GC-100"),
            (Reason: "invalid\u0001reason", Approval: "GC-100"),
            (Reason: "invalid\uD800reason", Approval: "GC-100"),
            (Reason: "Matter closed", Approval: new string('a', 257)),
            (Reason: "Matter closed", Approval: "bad\u0001approval"),
            (Reason: "Matter closed", Approval: " padded-approval "),
        };
        foreach (var invalid in invalidReleases)
        {
            await Assert.ThrowsAsync<LegalHoldException>(
                () => service.ReleaseAsync(
                    holdId,
                    actor,
                    invalid.Reason,
                    invalid.Approval));

            Assert.True(
                Assert.Single(
                    await service.ListAsync()).IsActive);
            Assert.Equal(
                baselineEventCount,
                (await events.ReadAllAsync()).Count);
        }
    }

    [Fact]
    public async Task Legal_hold_commands_require_authentication_and_cross_tenant_release_is_hidden()
    {
        var unauthenticated = await factory.CreateClient().PostAsJsonAsync("/api/trust/legal-holds", new
        {
            dataClass = "DomainEvents",
            reason = "Investigation",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var tenantA = AdminClient(Guid.NewGuid());
        var placed = await tenantA.PostAsJsonAsync("/api/trust/legal-holds", new
        {
            dataClass = "DomainEvents",
            reason = "Investigation",
        });
        var holdId = (await JsonDocument.ParseAsync(await placed.Content.ReadAsStreamAsync())).RootElement
            .GetProperty("holdId").GetGuid();
        var tenantB = AdminClient(Guid.NewGuid());
        tenantB.DefaultRequestHeaders.Add("X-Approval-Reference", "approval:1");
        var response = await tenantB.PostAsJsonAsync($"/api/trust/legal-holds/{holdId}/release", new { reason = "Attempt" });
        var nonexistent = await tenantB.PostAsJsonAsync(
            $"/api/trust/legal-holds/{Guid.NewGuid()}/release",
            new { reason = "Attempt" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(response.StatusCode, nonexistent.StatusCode);
        Assert.Equal(
            await response.Content.ReadAsStringAsync(),
            await nonexistent.Content.ReadAsStringAsync());
        Assert.DoesNotContain(holdId.ToString(), await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Asset_record_returns_404_for_an_unknown_asset()
    {
        var response = await TenantClient().GetAsync($"/api/portfolio/assets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Resolution & Merge Workbench (P6-T04) ----

    private HttpClient ClientFor(Guid tenant, Guid? objectId = null) =>
        factory.AuthenticatedClient(
            tenant,
            objectId: objectId,
            roles: [ControlTowerRole.Operator]);

    private HttpClient AdminClient(Guid tenant, Guid? objectId = null) =>
        factory.AuthenticatedClient(
            tenant,
            objectId: objectId,
            roles: [ControlTowerRole.Administrator]);

    private static async Task<string> ResolvedActorAsync(
        HttpClient client,
        Guid directoryTenant,
        Guid objectId)
    {
        var identity =
            await client.GetFromJsonAsync<WhoAmI>("/whoami");
        var actor = Assert.IsType<string>(identity!.Actor);
        Assert.StartsWith(
            "person:",
            actor,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            directoryTenant.ToString("D"),
            actor,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            objectId.ToString("D"),
            actor,
            StringComparison.OrdinalIgnoreCase);
        return actor;
    }

    private static ObservationDescriptor Obs(string value) =>
        new(Guid.NewGuid(), new NativeIdentifier("sys", "t", value), "Seeded", "agent", "Self-reported / Manual Import");

    /// <summary>Seeds two assets sharing identifier X (a collision) via the resolution service, in the tenant scope.</summary>
    private async Task<(Guid a, Guid b)> SeedCollisionAsync(Guid tenant)
    {
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var _ = accessor.BeginScope(new TenantId(tenant));
        var svc = scope.ServiceProvider.GetRequiredService<EntityResolutionService>();

        var a = (await svc.ResolveAsync(Obs("X"))).AssetId!.Value;
        var b = (await svc.ResolveAsync(Obs("Y"))).AssetId!.Value;
        await svc.ApproveManualLinkAsync(
            b,
            NativeIdentifierSet.Of(
                new NativeIdentifier("sys", "t", "X")),
            null,
            AuditActor.System("test-seed"));
        await svc.ResolveAsync(Obs("X")); // X now maps to A and B → collision → merge case
        return (a.Value, b.Value);
    }

    private sealed record CaseDto(Guid MergeCaseId, string Confidence, string Reason);

    private sealed record WhoAmI(string Tenant, string? Actor);

    [Fact]
    public async Task Merge_case_queue_lists_collisions_and_an_operator_can_resolve_them()
    {
        var tenant = Guid.NewGuid();
        await SeedCollisionAsync(tenant);
        var client = ClientFor(tenant);

        var cases = await client.GetFromJsonAsync<List<CaseDto>>("/api/resolution/merge-cases");
        Assert.NotNull(cases);
        var mergeCase = Assert.Single(cases!);
        Assert.Equal("Low", mergeCase.Confidence); // a collision is never auto-linked

        var resolve = await client.PostAsJsonAsync($"/api/resolution/merge-cases/{mergeCase.MergeCaseId}/resolve", new { outcome = "kept-separate" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        var after = await client.GetFromJsonAsync<List<CaseDto>>("/api/resolution/merge-cases");
        Assert.Empty(after!);
    }

    [Fact]
    public async Task Operator_merge_supersedes_source_links_visible_in_the_resolution_view()
    {
        var tenant = Guid.NewGuid();
        var (a, b) = await SeedCollisionAsync(tenant); // A←X, B←(Y,X)
        var client = ClientFor(tenant);

        var merge = await client.PostAsJsonAsync("/api/resolution/merge", new { targetId = a, sourceId = b });
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        // Source's links are superseded (retained, not deleted); target absorbed them.
        var sourceView = await client.GetStringAsync($"/api/resolution/assets/{b}");
        Assert.Contains("Superseded", sourceView);
        var targetView = await client.GetStringAsync($"/api/resolution/assets/{a}");
        Assert.Contains("Active", targetView);
    }

    [Fact]
    public async Task Resolution_view_is_404_for_an_unknown_asset()
    {
        var response = await ClientFor(Guid.NewGuid()).GetAsync($"/api/resolution/assets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
