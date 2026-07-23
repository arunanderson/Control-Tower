using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ControlTower.Host.Web.Authorization;
using ControlTower.Host.Web.Authentication;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Modules.Trust.Infrastructure;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Host.Web.Tests;

public class RoleAuthorizationTests(LocalJwtWebFactory factory)
    : IClassFixture<LocalJwtWebFactory>
{
    [Fact]
    public async Task Every_api_route_has_exactly_one_explicit_capability_policy()
    {
        var expected = ExpectedEndpointCapabilities();
        var capabilityPolicyNames = Enum
            .GetValues<ControlTowerCapability>()
            .Select(ControlTowerAuthorizationExtensions.PolicyName)
            .ToHashSet(StringComparer.Ordinal);
        var endpoints = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith(
                    "/api",
                    StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(27, endpoints.Count);
        var policies = factory.Services
            .GetRequiredService<IAuthorizationPolicyProvider>();
        foreach (var endpoint in endpoints)
        {
            var method = Assert.Single(
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
            var endpointKey =
                $"{method} {endpoint.RoutePattern.RawText}";
            var metadata = Assert.Single(
                endpoint.Metadata
                    .GetOrderedMetadata<RequiredControlTowerCapability>());
            Assert.True(
                expected.TryGetValue(endpointKey, out var expectedCapability),
                $"Unexpected Experience endpoint: {endpointKey}");
            Assert.Equal(expectedCapability, metadata.Capability);
            var policyName =
                ControlTowerAuthorizationExtensions.PolicyName(
                    metadata.Capability);
            var capabilityAuthorizationEntries = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Where(data =>
                    data.Policy is not null
                    && capabilityPolicyNames.Contains(data.Policy))
                .ToList();
            var capabilityAuthorization = Assert.Single(
                capabilityAuthorizationEntries);
            Assert.Equal(policyName, capabilityAuthorization.Policy);
            var policy = await policies.GetPolicyAsync(policyName);
            var requirement = Assert.Single(
                policy!.Requirements
                    .OfType<ControlTowerCapabilityRequirement>());
            Assert.Equal(metadata.Capability, requirement.Capability);
        }
    }

    [Fact]
    public void V1_roles_scopes_and_bundles_are_exact()
    {
        Assert.Equal(
        [
            ControlTowerRole.Viewer,
            ControlTowerRole.Operator,
            ControlTowerRole.Administrator,
            ControlTowerRole.ExecutiveScope,
        ], Enum.GetValues<ControlTowerRole>());
        Assert.Equal(
            [OrganizationScope.TenantWide],
            Enum.GetValues<OrganizationScope>());
        Assert.Equal(
            ["Viewer", "Operator", "Administrator", "Executive-scope"],
            Enum.GetValues<ControlTowerRole>()
                .Select(ControlTowerAccessCatalog.Name));
        Assert.Equal(
        [
            "portfolio.read",
            "economics.executive.read",
            "economics.portfolio.read",
            "economics.detail.read",
            "economics.reporting-periods.read",
            "economics.reporting-periods.manage",
            "governance.read",
            "trust.coverage.read",
            "trust.privileged-access.read",
            "trust.legal-holds.read",
            "trust.legal-holds.manage",
            "administration.read",
            "resolution.read",
            "resolution.manage",
            "ledger.manage",
        ], Enum.GetValues<ControlTowerCapability>()
            .Select(ControlTowerAccessCatalog.Name));

        var expected = new Dictionary<
            ControlTowerRole,
            ControlTowerCapability[]>
        {
            [ControlTowerRole.Viewer] =
            [
                ControlTowerCapability.PortfolioRead,
                ControlTowerCapability.EconomicsExecutiveRead,
                ControlTowerCapability.EconomicsPortfolioRead,
                ControlTowerCapability.EconomicsDetailRead,
                ControlTowerCapability.ReportingPeriodsRead,
                ControlTowerCapability.GovernanceRead,
                ControlTowerCapability.TrustCoverageRead,
                ControlTowerCapability.ResolutionRead,
            ],
            [ControlTowerRole.Operator] =
            [
                ControlTowerCapability.PortfolioRead,
                ControlTowerCapability.EconomicsExecutiveRead,
                ControlTowerCapability.EconomicsPortfolioRead,
                ControlTowerCapability.EconomicsDetailRead,
                ControlTowerCapability.ReportingPeriodsRead,
                ControlTowerCapability.ReportingPeriodsManage,
                ControlTowerCapability.GovernanceRead,
                ControlTowerCapability.TrustCoverageRead,
                ControlTowerCapability.ResolutionRead,
                ControlTowerCapability.ResolutionManage,
                ControlTowerCapability.LedgerManage,
            ],
            [ControlTowerRole.Administrator] =
            [
                ControlTowerCapability.TrustCoverageRead,
                ControlTowerCapability.PrivilegedAccessRead,
                ControlTowerCapability.LegalHoldsRead,
                ControlTowerCapability.LegalHoldsManage,
                ControlTowerCapability.AdministrationRead,
            ],
            [ControlTowerRole.ExecutiveScope] =
            [
                ControlTowerCapability.PortfolioRead,
                ControlTowerCapability.EconomicsExecutiveRead,
                ControlTowerCapability.EconomicsPortfolioRead,
                ControlTowerCapability.ReportingPeriodsRead,
                ControlTowerCapability.TrustCoverageRead,
            ],
        };

        foreach (var (role, capabilities) in expected)
        {
            var access = ControlTowerAccessCatalog.Resolve([role]);
            Assert.Equal(
                capabilities.OrderBy(capability => capability),
                access.Capabilities.OrderBy(capability => capability));
            Assert.Equal(OrganizationScope.TenantWide, access.OrganizationScope);
        }
    }

    [Fact]
    public async Task Unrecognised_role_values_cannot_be_assigned()
    {
        var tenant = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var tenants =
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var _ = tenants.BeginScope(new TenantId(tenant));
        var service =
            scope.ServiceProvider.GetRequiredService<RoleAssignmentService>();

        await Assert.ThrowsAsync<RoleAssignmentException>(
            () => service.AssignAsync(
                Guid.NewGuid(),
                (ControlTowerRole)999,
                RoleAssignmentActor.System("test")));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ControlTowerAccessCatalog.Name((ControlTowerRole)999));
    }

    [Fact]
    public async Task No_assignment_and_caller_supplied_grants_fail_closed()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var noAssignment = factory.AuthenticatedClient(
            tenant,
            objectId: actor,
            roles: []);
        noAssignment.DefaultRequestHeaders.Add("X-Role", "Administrator");
        noAssignment.DefaultRequestHeaders.Add(
            "X-Capability",
            "administration.read");

        var denied = await noAssignment.GetAsync("/api/admin/summary");

        await AssertGenericForbid(denied, tenant);

        var claimTenant = Guid.NewGuid();
        var claimActor = Guid.NewGuid();
        factory.AllowTenant(claimTenant, claimTenant);
        factory.AssignRoles(
            claimTenant,
            claimActor,
            [ControlTowerRole.Viewer]);
        var claims = LocalJwtWebFactory
            .HumanClaims(claimTenant, claimActor, "subject")
            .Concat(
            [
                new Claim("roles", "Administrator"),
                new Claim("groups", "ControlTower-Admins"),
                new Claim("capability", "administration.read"),
            ]);
        var tokenClient = factory.ClientWithToken(
            factory.IssueToken(
                claims,
                LocalJwtWebFactory.IssuerFor(claimTenant)));
        tokenClient.DefaultRequestHeaders.Add("X-Role", "Administrator");
        tokenClient.DefaultRequestHeaders.Add(
            "X-Capability",
            "administration.read");

        await AssertGenericForbid(
            await tokenClient.GetAsync("/api/admin/summary"),
            claimTenant);
        using var session = JsonDocument.Parse(
            await tokenClient.GetStringAsync("/whoami"));
        Assert.Equal(
            ["Viewer"],
            session.RootElement.GetProperty("roles")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray());
        Assert.DoesNotContain(
            "administration.read",
            session.RootElement.GetProperty("capabilities")
                .EnumerateArray()
                .Select(value => value.GetString()));
    }

    [Fact]
    public async Task Viewer_is_read_only_and_cannot_enter_administration()
    {
        var client = Client(ControlTowerRole.Viewer);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/portfolio/assets")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/governance/debt")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateReportingPeriod(client)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/admin/summary")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await PlaceLegalHold(client)).StatusCode);
    }

    [Fact]
    public async Task Operator_has_only_documented_operational_commands()
    {
        var client = Client(ControlTowerRole.Operator);

        Assert.Equal(
            HttpStatusCode.OK,
            (await CreateReportingPeriod(client)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/admin/providers")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/trust/privileged-access")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await PlaceLegalHold(client)).StatusCode);
    }

    [Fact]
    public async Task Administrator_has_platform_trust_actions_without_operator_inheritance()
    {
        var client = Client(ControlTowerRole.Administrator);
        client.DefaultRequestHeaders.Add("X-Purpose", "Security review");

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/admin/summary")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/trust/privileged-access")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await PlaceLegalHold(client)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateReportingPeriod(client)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await MergeUnknownAssets(client)).StatusCode);
    }

    [Fact]
    public async Task Executive_scope_is_restricted_to_prescribed_read_paths()
    {
        var client = Client(ControlTowerRole.ExecutiveScope);
        var allowed = new[]
        {
            "/api/portfolio/assets",
            "/api/economics/executive",
            "/api/economics/portfolio",
            "/api/economics/reporting-periods",
            "/api/trust/coverage",
        };
        foreach (var path in allowed)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync(path)).StatusCode);
        }

        foreach (var path in new[]
                 {
                     "/api/economics/departments",
                     "/api/governance/debt",
                     "/api/resolution/merge-cases",
                     "/api/admin/summary",
                 })
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.GetAsync(path)).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateReportingPeriod(client)).StatusCode);
    }

    [Fact]
    public async Task Multiple_assignments_union_only_curated_bundles()
    {
        var client = factory.AuthenticatedClient(
            Guid.NewGuid(),
            roles:
            [
                ControlTowerRole.Viewer,
                ControlTowerRole.Administrator,
            ]);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/portfolio/assets")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/admin/summary")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateReportingPeriod(client)).StatusCode);
    }

    [Fact]
    public async Task Same_oid_role_assignments_are_isolated_between_tenants()
    {
        var actor = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var adminA = factory.AuthenticatedClient(
            tenantA,
            objectId: actor,
            roles: [ControlTowerRole.Administrator]);
        var unassignedB = factory.AuthenticatedClient(
            tenantB,
            objectId: actor,
            roles: []);

        Assert.Equal(
            HttpStatusCode.OK,
            (await adminA.GetAsync("/api/admin/summary")).StatusCode);
        await AssertGenericForbid(
            await unassignedB.GetAsync("/api/admin/summary"),
            tenantB);
    }

    [Fact]
    public async Task Person_key_map_does_not_correlate_the_same_oid_across_tenants()
    {
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var objectId = Guid.NewGuid();
        var tenants = new TenantContextAccessor();
        var map = new InMemoryPersonKeyMap(tenants);
        PersonKey keyA;

        using (tenants.BeginScope(tenantA))
            keyA = await map.GetOrCreateAsync(objectId);

        using (tenants.BeginScope(tenantB))
        {
            Assert.Null(await map.FindAsync(objectId));
            var keyB = await map.GetOrCreateAsync(objectId);
            Assert.NotEqual(keyA, keyB);
            Assert.Equal(keyB, await map.FindAsync(objectId));
        }

        using (tenants.BeginScope(tenantA))
            Assert.Equal(keyA, await map.FindAsync(objectId));
    }

    [Fact]
    public async Task Effective_access_rechecks_subject_and_tenant_from_the_reader()
    {
        var currentTenant = new TenantId(Guid.NewGuid());
        var otherTenant = new TenantId(Guid.NewGuid());
        var requestedObjectId = Guid.NewGuid();
        var requestedPersonKey = new PersonKey(Guid.NewGuid());
        var otherPersonKey = new PersonKey(Guid.NewGuid());
        var system = RoleAssignmentActor.System("test");
        var assignments = new StaticRoleAssignmentReader(
            new RoleAssignment(
                Guid.NewGuid(),
                currentTenant,
                requestedPersonKey,
                ControlTowerRole.Viewer,
                system,
                DateTimeOffset.UtcNow),
            new RoleAssignment(
                Guid.NewGuid(),
                otherTenant,
                requestedPersonKey,
                ControlTowerRole.Administrator,
                system,
                DateTimeOffset.UtcNow),
            new RoleAssignment(
                Guid.NewGuid(),
                currentTenant,
                otherPersonKey,
                ControlTowerRole.Administrator,
                system,
                DateTimeOffset.UtcNow));
        var tenants = new TenantContextAccessor();
        using var _ = tenants.BeginScope(currentTenant);
        var resolver = new EffectiveAccessResolver(
            new StaticPersonKeyMap(requestedPersonKey),
            assignments,
            tenants);

        var access = await resolver.ResolveAsync(requestedObjectId);

        Assert.Equal([ControlTowerRole.Viewer], access.Roles);
        Assert.True(access.Allows(ControlTowerCapability.PortfolioRead));
        Assert.False(access.Allows(ControlTowerCapability.AdministrationRead));
    }

    [Fact]
    public async Task Default_person_keys_fail_closed_at_every_assignment_boundary()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var tenants = new TenantContextAccessor();
        using var _ = tenants.BeginScope(tenant);
        var reader = new StaticRoleAssignmentReader();
        var resolver = new EffectiveAccessResolver(
            new StaticPersonKeyMap(default),
            reader,
            tenants);

        var access = await resolver.ResolveAsync(Guid.NewGuid());

        Assert.Empty(access.Roles);
        Assert.Empty(access.Capabilities);
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignmentActor.Person(default));
        Assert.Throws<RoleAssignmentException>(
            () => new RoleAssignment(
                Guid.NewGuid(),
                tenant,
                default,
                ControlTowerRole.Viewer,
                RoleAssignmentActor.System("test"),
                DateTimeOffset.UtcNow));

        var store = new LeakyRoleAssignmentStore(null, []);
        var service = new RoleAssignmentService(
            new StaticPersonKeyMap(default),
            store,
            tenants);
        await Assert.ThrowsAsync<RoleAssignmentException>(
            () => service.AssignAsync(
                Guid.NewGuid(),
                ControlTowerRole.Viewer,
                RoleAssignmentActor.System("test")));
        Assert.Null(store.Committed);
    }

    [Fact]
    public async Task Assignment_service_rechecks_adapter_tenant_on_assign_and_revoke()
    {
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var subjectKey = new PersonKey(Guid.NewGuid());
        var foreign = new RoleAssignment(
            Guid.NewGuid(),
            tenantB,
            subjectKey,
            ControlTowerRole.Administrator,
            RoleAssignmentActor.System("foreign"),
            DateTimeOffset.UtcNow);
        var otherSubject = new RoleAssignment(
            Guid.NewGuid(),
            tenantA,
            new PersonKey(Guid.NewGuid()),
            ControlTowerRole.Administrator,
            RoleAssignmentActor.System("other-subject"),
            DateTimeOffset.UtcNow);
        var tenants = new TenantContextAccessor();
        using var _ = tenants.BeginScope(tenantA);
        var leakyAssignStore = new LeakyRoleAssignmentStore(
            null,
            [foreign, otherSubject]);
        var service = new RoleAssignmentService(
            new StaticPersonKeyMap(subjectKey),
            leakyAssignStore,
            tenants);

        var assigned = await service.AssignAsync(
            Guid.NewGuid(),
            ControlTowerRole.Administrator,
            RoleAssignmentActor.System("tenant-a"));

        Assert.NotEqual(foreign.Id, assigned);
        Assert.Equal(tenantA, leakyAssignStore.Committed!.Tenant);

        var leakyRevokeStore = new LeakyRoleAssignmentStore(
            foreign,
            []);
        var revokeService = new RoleAssignmentService(
            new StaticPersonKeyMap(subjectKey),
            leakyRevokeStore,
            tenants);
        var exception = await Assert.ThrowsAsync<RoleAssignmentException>(
            () => revokeService.RevokeAsync(
                foreign.Id,
                RoleAssignmentActor.System("tenant-a")));
        Assert.Equal(
            "Role assignment not found in this tenant.",
            exception.Message);
        Assert.Null(leakyRevokeStore.Committed);
        Assert.True(foreign.IsActive);

        var wrongIdAssignment = new RoleAssignment(
            Guid.NewGuid(),
            tenantA,
            subjectKey,
            ControlTowerRole.Viewer,
            RoleAssignmentActor.System("tenant-a"),
            DateTimeOffset.UtcNow);
        var wrongIdStore = new LeakyRoleAssignmentStore(
            wrongIdAssignment,
            []);
        var wrongIdService = new RoleAssignmentService(
            new StaticPersonKeyMap(subjectKey),
            wrongIdStore,
            tenants);
        exception = await Assert.ThrowsAsync<RoleAssignmentException>(
            () => wrongIdService.RevokeAsync(
                Guid.NewGuid(),
                RoleAssignmentActor.System("tenant-a")));
        Assert.Equal(
            "Role assignment not found in this tenant.",
            exception.Message);
        Assert.Null(wrongIdStore.Committed);
        Assert.True(wrongIdAssignment.IsActive);
    }

    [Fact]
    public async Task Development_store_rejects_mismatched_assignment_audit_evidence()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var tenants = new TenantContextAccessor();
        using var _ = tenants.BeginScope(tenant);
        var events = new CapturingEventStore();
        var store = new InMemoryRoleAssignmentStore(tenants, events);
        var assignedAt = DateTimeOffset.UtcNow;
        var actor = RoleAssignmentActor.System("test");
        var assignment = new RoleAssignment(
            Guid.NewGuid(),
            tenant,
            new PersonKey(Guid.NewGuid()),
            ControlTowerRole.Viewer,
            actor,
            assignedAt);
        var canonical = new RoleAssignmentChanged
        {
            AssignmentId = assignment.Id,
            SubjectPersonKey = assignment.SubjectPersonKey.Value,
            Role = "Viewer",
            OrganizationScope = "TenantWide",
            Change = "Assigned",
            ChangedBy = actor.ToString(),
            OccurredAt = assignedAt,
        };
        RoleAssignmentChanged[] mismatches =
        [
            canonical with { EventId = Guid.Empty },
            canonical with { Change = "Revoked" },
            canonical with
            {
                ChangedBy = RoleAssignmentActor.System("forged").ToString(),
            },
            canonical with { OccurredAt = assignedAt.AddSeconds(1) },
        ];

        foreach (var mismatch in mismatches)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.CommitAsync(assignment, mismatch));
        }

        Assert.Equal(0, events.AppendCount);
        Assert.Null(await store.GetAsync(assignment.Id));
        await Assert.ThrowsAsync<NotSupportedException>(
            () => store.CommitAsync(assignment, canonical));
        Assert.Equal(1, events.AppendCount);
        Assert.Null(await store.GetAsync(assignment.Id));
    }

    [Fact]
    public async Task Denied_privileged_read_stops_before_purpose_and_audit()
    {
        var tenant = Guid.NewGuid();
        var client = factory.AuthenticatedClient(
            tenant,
            roles: [ControlTowerRole.Viewer]);

        var response =
            await client.GetAsync("/api/trust/privileged-access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var tenants =
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var _ = tenants.BeginScope(new TenantId(tenant));
        Assert.Empty(
            await scope.ServiceProvider
                .GetRequiredService<IPrivilegedAccessProjection>()
                .ListAsync());
        var eventPayloads = (await scope.ServiceProvider
                .GetRequiredService<IEventStore>()
                .ReadAllAsync())
            .Select(stored => Encoding.UTF8.GetString(stored.Payload));
        Assert.DoesNotContain(
            eventPayloads,
            payload =>
                payload.Contains(
                    "trust.privileged-access-log",
                    StringComparison.OrdinalIgnoreCase)
                || payload.Contains(
                    "PrivilegedReadRecorded",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task Role_assignment_changes_are_evented_and_revocation_removes_access()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        factory.AllowTenant(tenant, tenant);
        Guid assignmentId;
        using (var scope = factory.Services.CreateScope())
        {
            var tenants =
                scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            using var _ = tenants.BeginScope(new TenantId(tenant));
            var service =
                scope.ServiceProvider.GetRequiredService<RoleAssignmentService>();
            assignmentId = await service.AssignAsync(
                actor,
                ControlTowerRole.Administrator,
                RoleAssignmentActor.System("test-assign"));
        }

        var client = factory.ClientWithToken(
            factory.IssueHumanToken(tenant, actor, "subject"));
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/admin/summary")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var tenants =
                scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            using var _ = tenants.BeginScope(new TenantId(tenant));
            var service =
                scope.ServiceProvider.GetRequiredService<RoleAssignmentService>();
            await service.RevokeAsync(
                assignmentId,
                RoleAssignmentActor.System("test-revoke"));
        }

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/admin/summary")).StatusCode);

        using var readScope = factory.Services.CreateScope();
        var accessor =
            readScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var tenantScope = accessor.BeginScope(new TenantId(tenant));
        var events = await readScope.ServiceProvider
            .GetRequiredService<IEventStore>()
            .ReadAllAsync();
        Assert.Equal(2, events.Count);
        var changes = events
            .Select(stored => new
            {
                Stored = stored,
                Event = JsonSerializer.Deserialize<RoleAssignmentChanged>(
                    stored.Payload)!,
            })
            .ToList();
        Assert.All(
            changes,
            change =>
            {
                Assert.NotEqual(Guid.Empty, change.Event.EventId);
                Assert.Equal(change.Stored.EventId, change.Event.EventId);
                Assert.NotEqual(default, change.Event.OccurredAt);
                Assert.Equal(change.Stored.OccurredAt, change.Event.OccurredAt);
                Assert.NotEqual(Guid.Empty, change.Event.SubjectPersonKey);
            });
        var assignedChange = Assert.Single(
            changes,
            change => change.Event.Change == "Assigned");
        var revokedChange = Assert.Single(
            changes,
            change => change.Event.Change == "Revoked");
        Assert.Equal("system:test-assign", assignedChange.Event.ChangedBy);
        Assert.Equal("system:test-revoke", revokedChange.Event.ChangedBy);
        Assert.Equal(
            assignedChange.Event.SubjectPersonKey,
            revokedChange.Event.SubjectPersonKey);
        var payloads = events
            .Select(stored => Encoding.UTF8.GetString(stored.Payload))
            .ToList();
        Assert.Contains(
            payloads,
            payload => payload.Contains("\"Change\":\"Assigned\""));
        Assert.Contains(
            payloads,
            payload => payload.Contains("\"Change\":\"Revoked\""));
        Assert.All(
            payloads,
            payload =>
            {
                Assert.Contains(
                    assignmentId.ToString("D"),
                    payload,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"Role\":\"Administrator\"", payload);
                Assert.Contains("\"OrganizationScope\":\"TenantWide\"", payload);
                Assert.Contains("\"SubjectPersonKey\":", payload);
                Assert.DoesNotContain(
                    actor.ToString("D"),
                    payload,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    "\"SubjectObjectId\"",
                    payload,
                    StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "entra:",
                    payload,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public async Task Whoami_exposes_only_server_resolved_effective_access()
    {
        var client = factory.AuthenticatedClient(
            Guid.NewGuid(),
            roles:
            [
                ControlTowerRole.Viewer,
                ControlTowerRole.Administrator,
            ]);

        using var response = await client.GetAsync("/whoami");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(
            "TenantWide",
            root.GetProperty("organizationScope").GetString());
        var roles = root.GetProperty("roles")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToList();
        Assert.Equal(["Viewer", "Administrator"], roles);
        var capabilities = root.GetProperty("capabilities")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("portfolio.read", capabilities);
        Assert.Contains("administration.read", capabilities);
        Assert.DoesNotContain(
            "economics.reporting-periods.manage",
            capabilities);
    }

    [Fact]
    public void Ledger_bridge_maps_operator_access_and_binds_the_tenant()
    {
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var objectId = Guid.NewGuid();
        var http = new DefaultHttpContext();
        typeof(AuthenticatedHumanContext)
            .GetMethod(
                "Set",
                BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(
                null,
                [
                    http,
                    new AuthenticatedHuman(
                        Guid.NewGuid(),
                        objectId,
                        "subject"),
                ]);
        var httpAccessor = new HttpContextAccessor { HttpContext = http };
        var tenants = new TenantContextAccessor();
        var current = new CurrentEffectiveAccess();
        current.Set(
            tenantA,
            objectId,
            ControlTowerAccessCatalog.Resolve([ControlTowerRole.Operator]));
        var authorizer = new HttpContextLedgerAuthorizer(
            httpAccessor,
            tenants,
            current);

        using (tenants.BeginScope(tenantA))
        {
            Assert.True(authorizer.IsAllowed(LedgerCapability.TriageAssets));
            Assert.True(authorizer.IsAllowed(LedgerCapability.RegisterAssets));
            Assert.True(authorizer.IsAllowed(LedgerCapability.RetireAssets));
        }

        using (tenants.BeginScope(tenantB))
            Assert.False(authorizer.IsAllowed(LedgerCapability.TriageAssets));

        current.Set(
            tenantA,
            objectId,
            ControlTowerAccessCatalog.Resolve([ControlTowerRole.Viewer]));
        using (tenants.BeginScope(tenantA))
            Assert.False(authorizer.IsAllowed(LedgerCapability.TriageAssets));
    }

    [Fact]
    public void Web_host_replaces_the_permissive_ledger_authorizer()
    {
        using var scope = factory.Services.CreateScope();
        var authorizer =
            scope.ServiceProvider.GetRequiredService<ILedgerAuthorizer>();

        Assert.IsType<HttpContextLedgerAuthorizer>(authorizer);
        Assert.False(authorizer.IsAllowed(LedgerCapability.TriageAssets));
        Assert.False(authorizer.IsAllowed(LedgerCapability.RegisterAssets));
        Assert.False(authorizer.IsAllowed(LedgerCapability.RetireAssets));
    }

    private HttpClient Client(ControlTowerRole role) =>
        factory.AuthenticatedClient(Guid.NewGuid(), roles: [role]);

    private static Task<HttpResponseMessage> CreateReportingPeriod(
        HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/economics/reporting-periods",
            new
            {
                start = "2026-06-01T00:00:00Z",
                end = "2026-07-01T00:00:00Z",
            });

    private static Task<HttpResponseMessage> PlaceLegalHold(
        HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/trust/legal-holds",
            new { dataClass = "All", reason = "Investigation" });

    private static Task<HttpResponseMessage> MergeUnknownAssets(
        HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/resolution/merge",
            new { targetId = Guid.NewGuid(), sourceId = Guid.NewGuid() });

    private static async Task AssertGenericForbid(
        HttpResponseMessage response,
        Guid tenant)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            tenant.ToString("D"),
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "role",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "capability",
            body,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticPersonKeyMap(PersonKey key) : IPersonKeyMap
    {
        public Task<PersonKey?> FindAsync(
            Guid directoryObjectId,
            CancellationToken ct = default) =>
            Task.FromResult<PersonKey?>(key);

        public Task<PersonKey> GetOrCreateAsync(
            Guid directoryObjectId,
            CancellationToken ct = default) =>
            Task.FromResult(key);
    }

    private sealed class StaticRoleAssignmentReader(
        params RoleAssignment[] assignments) : IRoleAssignmentReader
    {
        public Task<IReadOnlyList<RoleAssignment>> ListForSubjectAsync(
            PersonKey subjectPersonKey,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RoleAssignment>>(assignments);
    }

    private sealed class LeakyRoleAssignmentStore(
        RoleAssignment? assignment,
        IReadOnlyList<RoleAssignment> assignments) : IRoleAssignmentStore
    {
        public RoleAssignment? Committed { get; private set; }

        public Task<RoleAssignment?> GetAsync(
            Guid assignmentId,
            CancellationToken ct = default) =>
            Task.FromResult(assignment);

        public Task<IReadOnlyList<RoleAssignment>> ListForSubjectAsync(
            PersonKey subjectPersonKey,
            CancellationToken ct = default) =>
            Task.FromResult(assignments);

        public Task CommitAsync(
            RoleAssignment roleAssignment,
            RoleAssignmentChanged changed,
            CancellationToken ct = default)
        {
            Committed = roleAssignment;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingEventStore : IEventStore
    {
        public int AppendCount { get; private set; }

        public ValueTask<StoredEvent> AppendAsync(
            IDomainEvent @event,
            ReadOnlyMemory<byte> payload,
            CancellationToken ct = default)
        {
            AppendCount++;
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
            CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
    }

    private static IReadOnlyDictionary<string, ControlTowerCapability>
        ExpectedEndpointCapabilities() =>
        new Dictionary<string, ControlTowerCapability>
        {
            ["GET /api/portfolio/assets"] =
                ControlTowerCapability.PortfolioRead,
            ["GET /api/portfolio/assets/{id:guid}"] =
                ControlTowerCapability.PortfolioRead,
            ["GET /api/economics/executive"] =
                ControlTowerCapability.EconomicsExecutiveRead,
            ["GET /api/economics/portfolio"] =
                ControlTowerCapability.EconomicsPortfolioRead,
            ["GET /api/economics/departments"] =
                ControlTowerCapability.EconomicsDetailRead,
            ["GET /api/economics/agents"] =
                ControlTowerCapability.EconomicsDetailRead,
            ["GET /api/economics/reporting-periods"] =
                ControlTowerCapability.ReportingPeriodsRead,
            ["GET /api/economics/reporting-periods/{id:guid}/snapshots"] =
                ControlTowerCapability.ReportingPeriodsRead,
            ["POST /api/economics/reporting-periods"] =
                ControlTowerCapability.ReportingPeriodsManage,
            ["POST /api/economics/reporting-periods/{id:guid}/closing"] =
                ControlTowerCapability.ReportingPeriodsManage,
            ["POST /api/economics/reporting-periods/{id:guid}/freeze"] =
                ControlTowerCapability.ReportingPeriodsManage,
            ["POST /api/economics/reporting-periods/{id:guid}/restate"] =
                ControlTowerCapability.ReportingPeriodsManage,
            ["GET /api/governance/cases"] =
                ControlTowerCapability.GovernanceRead,
            ["GET /api/governance/debt"] =
                ControlTowerCapability.GovernanceRead,
            ["GET /api/trust/coverage"] =
                ControlTowerCapability.TrustCoverageRead,
            ["GET /api/trust/privileged-access"] =
                ControlTowerCapability.PrivilegedAccessRead,
            ["GET /api/trust/legal-holds"] =
                ControlTowerCapability.LegalHoldsRead,
            ["POST /api/trust/legal-holds"] =
                ControlTowerCapability.LegalHoldsManage,
            ["POST /api/trust/legal-holds/{id:guid}/release"] =
                ControlTowerCapability.LegalHoldsManage,
            ["GET /api/admin/summary"] =
                ControlTowerCapability.AdministrationRead,
            ["GET /api/admin/providers"] =
                ControlTowerCapability.AdministrationRead,
            ["GET /api/resolution/merge-cases"] =
                ControlTowerCapability.ResolutionRead,
            ["GET /api/resolution/assets/{id:guid}"] =
                ControlTowerCapability.ResolutionRead,
            ["POST /api/resolution/merge"] =
                ControlTowerCapability.ResolutionManage,
            ["POST /api/resolution/split"] =
                ControlTowerCapability.ResolutionManage,
            ["POST /api/resolution/manual-link"] =
                ControlTowerCapability.ResolutionManage,
            ["POST /api/resolution/merge-cases/{id:guid}/resolve"] =
                ControlTowerCapability.ResolutionManage,
        };
}
