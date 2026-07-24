using System.Reflection;
using ControlTower.Adapters.InMemory;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;

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
            (typeof(Modules.Trust.Authorization.PersonKeyMapChanged), "PersonKeyMapChanged", EventPrivilege.Privileged),
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

    private static readonly HashSet<string> ActorPresentationMembers =
    [
        MemberKey(
            typeof(Modules.Audit.LegalHoldView),
            nameof(Modules.Audit.LegalHoldView.PlacedBy)),
        MemberKey(
            typeof(Modules.Audit.LegalHoldView),
            nameof(Modules.Audit.LegalHoldView.ReleasedBy)),
        MemberKey(
            typeof(Modules.Economics.Application.ReportingPeriodView),
            nameof(Modules.Economics.Application.ReportingPeriodView.FrozenBy)),
        MemberKey(
            typeof(Modules.Economics.Application.ReportSnapshotView),
            nameof(Modules.Economics.Application.ReportSnapshotView.SignedBy)),
        MemberKey(
            typeof(Modules.Ledger.Application.ResolutionLinkView),
            nameof(Modules.Ledger.Application.ResolutionLinkView.LinkedBy)),
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
            typeof(Modules.Trust.Authorization.PersonKeyMapChanged),
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

    [Fact]
    public void Every_actor_slot_uses_the_shared_audit_actor()
    {
        var violations = new List<string>();
        var types = ProductionContractTypes();

        foreach (var property in types.SelectMany(type =>
                     type.GetProperties(
                         BindingFlags.Public
                         | BindingFlags.Instance
                         | BindingFlags.DeclaredOnly)))
        {
            if (!IsActorSlot(
                    property.Name,
                    property.PropertyType)
                || ActorPresentationMembers.Contains(
                    MemberKey(property)))
            {
                continue;
            }

            if (SemanticType(property.PropertyType)
                != typeof(AuditActor))
            {
                violations.Add(
                    $"{MemberKey(property)} uses "
                    + property.PropertyType.FullName);
            }
        }

        foreach (var method in types.SelectMany(type =>
                     type.GetMethods(
                         BindingFlags.Public
                         | BindingFlags.Instance
                         | BindingFlags.Static
                         | BindingFlags.DeclaredOnly))
                 .Where(method =>
                     !method.IsSpecialName
                     && method.Name != "Deconstruct"))
        {
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.Name is not null
                    && IsActorSlot(
                        parameter.Name,
                        parameter.ParameterType)
                    && SemanticType(parameter.ParameterType)
                    != typeof(AuditActor))
                {
                    violations.Add(
                        $"{method.DeclaringType!.FullName}"
                        + $".{method.Name}({parameter.Name}) uses "
                        + parameter.ParameterType.FullName);
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Every actor slot must use AuditActor: "
            + string.Join("; ", violations));
    }

    [Fact]
    public void No_alternate_type_duplicates_the_audit_actor_shape()
    {
        var duplicates = ProductionContractTypes()
            .Where(type =>
                type != typeof(AuditActor)
                && !type.IsEnum)
            .Where(HasAuditActorShape)
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Person_references_are_opaque_outside_e19()
    {
        var properties = ProductionContractTypes()
            .SelectMany(type =>
                type.GetProperties(
                    BindingFlags.Public
                    | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly))
            .ToArray();
        var violations = properties
            .Where(property =>
                property.Name.EndsWith(
                    "PersonKey",
                    StringComparison.Ordinal))
            .Where(property =>
                SemanticType(property.PropertyType)
                != typeof(PersonKey))
            .Select(property =>
                $"{MemberKey(property)} uses "
                + property.PropertyType.FullName)
            .ToList();

        var rawIdentityNames = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "DirectoryObjectId",
            "SubjectObjectId",
            "DisplaySnapshot",
            "OwnerDisplayName",
            "Email",
            "UserPrincipalName",
        };
        var allowedRawIdentityMembers =
            new HashSet<string>(StringComparer.Ordinal)
            {
                MemberKey(
                    typeof(Modules.Trust.Authorization
                        .DirectoryIdentitySnapshot),
                    nameof(Modules.Trust.Authorization
                        .DirectoryIdentitySnapshot
                        .DirectoryObjectId)),
                MemberKey(
                    typeof(Modules.Trust.Authorization
                        .DirectoryIdentitySnapshot),
                    nameof(Modules.Trust.Authorization
                        .DirectoryIdentitySnapshot
                        .DisplaySnapshot)),
                MemberKey(
                    typeof(Modules.Ledger.Application
                        .AssetLedgerView),
                    nameof(Modules.Ledger.Application
                        .AssetLedgerView.OwnerDisplayName)),
            };

        violations.AddRange(
            properties
                .Where(property =>
                    rawIdentityNames.Contains(
                        property.Name))
                .Where(property =>
                    !allowedRawIdentityMembers.Contains(
                        MemberKey(property)))
                .Select(property =>
                    $"{MemberKey(property)} exposes raw person identity"));

        var personRefProperties =
            typeof(Modules.Ledger.Domain.PersonRef)
                .GetProperties(
                    BindingFlags.Public
                    | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly);
        Assert.Equal(
            new[] { typeof(PersonKey) },
            personRefProperties
                .Select(property => property.PropertyType));

        var personRefConstructor = Assert.Single(
            typeof(Modules.Ledger.Domain.PersonRef)
                .GetConstructors());
        Assert.Equal(
            new[] { typeof(PersonKey) },
            personRefConstructor.GetParameters()
                .Select(parameter => parameter.ParameterType));

        Assert.True(
            violations.Count == 0,
            "Raw or untyped person references found: "
            + string.Join("; ", violations));
    }

    private static IReadOnlyList<Type> ProductionContractTypes() =>
        ProductionModuleAssemblies
            .Append(typeof(AuditActor).Assembly)
            .Append(typeof(InMemoryEventStore).Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.IsPublic
                || type.IsNestedPublic)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static bool IsActorSlot(
        string name,
        Type type)
    {
        var semanticType = SemanticType(type);
        if (semanticType == typeof(DateTimeOffset)
            || name.Equals(
                "DueBy",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Equals(
                "Actor",
                StringComparison.OrdinalIgnoreCase)
            || name.Equals(
                "By",
                StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(
                "Actor",
                StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(
                "By",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAuditActorShape(Type type)
    {
        var properties = type.GetProperties(
            BindingFlags.Public
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly);
        var hasActorKind = properties.Any(property =>
        {
            var candidate = SemanticType(
                property.PropertyType);
            if (!candidate.IsEnum)
                return false;

            var names = Enum.GetNames(candidate);
            return names.Contains(
                    nameof(AuditActorKind.Human),
                    StringComparer.Ordinal)
                && names.Contains(
                    nameof(AuditActorKind.System),
                    StringComparer.Ordinal)
                && names.Contains(
                    nameof(AuditActorKind.Provider),
                    StringComparer.Ordinal);
        });
        var identifierNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Id",
            "OpaqueId",
            "Identifier",
            "Value",
        };
        var hasIdentifier = properties.Any(property =>
            identifierNames.Contains(property.Name)
            && SemanticType(property.PropertyType)
                is var candidate
            && (candidate == typeof(string)
                || candidate == typeof(Guid)));

        return hasActorKind && hasIdentifier;
    }

    private static Type SemanticType(Type type) =>
        Nullable.GetUnderlyingType(type) ?? type;

    private static string MemberKey(MemberInfo member) =>
        MemberKey(member.DeclaringType!, member.Name);

    private static string MemberKey(
        Type declaringType,
        string memberName) =>
        $"{declaringType.FullName}.{memberName}";

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
