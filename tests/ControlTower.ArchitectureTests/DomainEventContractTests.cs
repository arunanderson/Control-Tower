using System.Reflection;
using ControlTower.Platform.Events;

namespace ControlTower.ArchitectureTests;

public sealed class DomainEventContractTests
{
    private static readonly (Type EventType, string CanonicalName, EventPrivilege Privilege)[]
        ExpectedContracts =
        [
            (typeof(Modules.Ledger.Domain.AssetDiscovered), "AssetDiscovered", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.AssetTriaged), "AssetTriaged", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.AssetRegistered), "AssetRegistered", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.AssetRejected), "AssetRejected", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.AssetRetired), "AssetRetired", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.LifecycleStateChanged), "LifecycleStateChanged", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.OwnershipAssigned), "OwnershipAssigned", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.OwnershipLapsed), "OwnershipLapsed", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.OwnershipReassigned), "OwnershipReassigned", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.ResolutionLinkAdded), "ResolutionLinkAdded", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.ResolutionLinkSevered), "ResolutionLinkRemoved", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.ResolutionLinkSuperseded), "ResolutionLinkSuperseded", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.MatchConfidenceChanged), "MatchConfidenceChanged", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.AssetMergedInto), "AssetsMerged", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.AssetSplit), "AssetSplit", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.MergeCaseOpened), "MergeCaseOpened", EventPrivilege.Standard),
            (typeof(Modules.Ledger.Domain.MergeCaseResolved), "MergeCaseDecided", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.CaseOpened), "CaseOpened", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.DecisionRecorded), "DecisionRecorded", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.CaseApproved), "CaseApproved", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.CaseRejected), "CaseRejected", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.WaiverGranted), "WaiverGranted", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.CaseExpired), "CaseExpired", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.RecertificationCompleted), "RecertificationCompleted", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.RetirementRequested), "RetirementRequested", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.ReuseDecisionRecorded), "ReuseDecisionRecorded", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.NotificationIntentRaised), "NotificationIntentRaised", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.NativeControlRequested), "NativeControlRequested", EventPrivilege.Standard),
            (typeof(Modules.Governance.Domain.GovernanceDebtRaised), "GovernanceDebtRaised", EventPrivilege.Standard),
            (typeof(Modules.Economics.Domain.CostObserved), "CostFactIngested", EventPrivilege.Standard),
            (typeof(Modules.Economics.Domain.ValueDeclared), "ValueDeclared", EventPrivilege.Standard),
            (typeof(Modules.Economics.Domain.ValueRevisedEvent), "ValueRevised", EventPrivilege.Standard),
            (typeof(Modules.Economics.Domain.ReportingPeriodFrozen), "ReportingPeriodFrozen", EventPrivilege.Standard),
            (typeof(Modules.Economics.Domain.ReportingPeriodRestated), "ReportingPeriodRestated", EventPrivilege.Standard),
            (typeof(Modules.Providers.Domain.ObservationIngested), "ObservationIngested", EventPrivilege.Standard),
            (typeof(Modules.Providers.Domain.ProviderCoverageUpdated), "ProviderCoverageUpdated", EventPrivilege.Standard),
            (typeof(Modules.Providers.Domain.ProviderSweepRequested), "ProviderSweepRequested", EventPrivilege.Standard),
            (typeof(Modules.Trust.Authorization.RoleAssignmentChanged), "RoleAssignmentChanged", EventPrivilege.Privileged),
            (typeof(Modules.Audit.PrivilegedReadRecorded), "PrivilegedReadRecorded", EventPrivilege.Privileged),
            (typeof(Modules.Audit.LegalHoldPlaced), "LegalHoldPlaced", EventPrivilege.Privileged),
            (typeof(Modules.Audit.LegalHoldReleased), "LegalHoldReleased", EventPrivilege.Privileged),
        ];

    private static readonly Assembly[] ProductionModuleAssemblies =
    [
        typeof(Modules.Ledger.LedgerModule).Assembly,
        typeof(Modules.Governance.GovernanceModule).Assembly,
        typeof(Modules.Economics.EconomicsModule).Assembly,
        typeof(Modules.Providers.ProvidersModule).Assembly,
        typeof(Modules.EnterpriseContext.EnterpriseContextModule).Assembly,
        typeof(Modules.Experience.ExperienceModule).Assembly,
        typeof(Modules.Trust.TrustModule).Assembly,
        typeof(Modules.Audit.AuditModule).Assembly,
    ];

    [Fact]
    public void Every_concrete_production_event_has_exactly_one_explicit_valid_declaration()
    {
        var eventTypes = ConcreteProductionEventTypes();

        Assert.Equal(
            ExpectedContracts
                .Select(expected => expected.EventType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal),
            eventTypes);

        foreach (var expected in ExpectedContracts)
        {
            var declarations = expected.EventType
                .GetCustomAttributes<DomainEventContractAttribute>(inherit: false)
                .ToArray();

            Assert.Single(declarations);
            var contract = DomainEventContracts.Resolve(expected.EventType);

            Assert.Equal(expected.CanonicalName, contract.EventType);
            Assert.Equal(expected.Privilege, contract.Privilege);
        }
    }

    [Fact]
    public void Canonical_event_names_are_globally_unique()
    {
        var duplicateNames = ConcreteProductionEventTypes()
            .Select(eventType => new
            {
                EventType = eventType,
                Contract = DomainEventContracts.Resolve(eventType),
            })
            .GroupBy(entry => entry.Contract.EventType, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key}: {string.Join(", ", group.Select(entry => entry.EventType.FullName))}")
            .ToArray();

        Assert.True(
            duplicateNames.Length == 0,
            $"Canonical event names must be globally unique. Duplicates: {string.Join("; ", duplicateNames)}");
    }

    [Fact]
    public void Privileged_event_set_is_exact()
    {
        var expected = new[]
        {
            typeof(Modules.Trust.Authorization.RoleAssignmentChanged),
            typeof(Modules.Audit.PrivilegedReadRecorded),
            typeof(Modules.Audit.LegalHoldPlaced),
            typeof(Modules.Audit.LegalHoldReleased),
        }
        .Select(type => type.FullName!)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

        var actual = ConcreteProductionEventTypes()
            .Where(eventType =>
                DomainEventContracts.Resolve(eventType).Privilege == EventPrivilege.Privileged)
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Platform_owns_the_only_stored_event_model()
    {
        var storedEventModels = ProductionModuleAssemblies
            .Append(typeof(StoredEvent).Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name == nameof(StoredEvent))
            .ToArray();

        Assert.Equal(new[] { typeof(StoredEvent) }, storedEventModels);
        Assert.Equal(
            typeof(StoredEvent).Assembly,
            typeof(IEventStore).Assembly);
    }

    private static IReadOnlyList<Type> ConcreteProductionEventTypes() =>
        ProductionModuleAssemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && typeof(IDomainEvent).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
}
