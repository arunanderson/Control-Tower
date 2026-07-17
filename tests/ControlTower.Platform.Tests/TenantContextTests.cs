using System;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Platform.Tests;

public class TenantContextTests
{
    [Fact]
    public void Accessing_current_without_a_scope_throws()
    {
        var accessor = new TenantContextAccessor();
        Assert.False(accessor.HasTenant);
        Assert.Throws<InvalidOperationException>(() => _ = accessor.Current);
    }

    [Fact]
    public void Within_a_scope_current_is_the_tenant_and_reverts_after_dispose()
    {
        var accessor = new TenantContextAccessor();
        var tenant = new TenantId(Guid.NewGuid());

        using (accessor.BeginScope(tenant))
        {
            Assert.True(accessor.HasTenant);
            Assert.Equal(tenant, accessor.Current);
        }

        Assert.False(accessor.HasTenant);
        Assert.Throws<InvalidOperationException>(() => _ = accessor.Current);
    }

    [Fact]
    public void Nested_scopes_restore_the_outer_tenant()
    {
        var accessor = new TenantContextAccessor();
        var outer = new TenantId(Guid.NewGuid());
        var inner = new TenantId(Guid.NewGuid());

        using (accessor.BeginScope(outer))
        {
            using (accessor.BeginScope(inner))
            {
                Assert.Equal(inner, accessor.Current);
            }
            Assert.Equal(outer, accessor.Current);
        }
    }

    [Fact]
    public void Empty_tenant_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new TenantId(Guid.Empty));
    }
}
