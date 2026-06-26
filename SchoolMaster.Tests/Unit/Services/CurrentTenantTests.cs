using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using SchoolMaster.Domain.CustomException;
using Xunit;

namespace SchoolMaster.Tests.Unit.Services;

// Guards the tenant-isolation fix: for an authenticated request the tenant comes from the JWT, never
// the attacker-controlled X-Tenant-Subdomain header. CurrentTenant lives in the global namespace.
public class CurrentTenantTests
{
    private static IHttpContextAccessor Accessor(HttpContext ctx)
    {
        var mock = new Mock<IHttpContextAccessor>();
        mock.SetupGet(a => a.HttpContext).Returns(ctx);
        return mock.Object;
    }

    private static HttpContext AuthenticatedContext(Guid tokenTenant, Guid? headerTenant = null)
    {
        var ctx = new DefaultHttpContext();
        // Passing an authenticationType makes Identity.IsAuthenticated return true.
        var identity = new ClaimsIdentity(
            new[] { new Claim("tenant_id", tokenTenant.ToString()) }, "TestAuth");
        ctx.User = new ClaimsPrincipal(identity);
        if (headerTenant.HasValue) ctx.Items["TenantId"] = headerTenant.Value;
        return ctx;
    }

    [Fact]
    public void Id_WhenAuthenticated_ReturnsTokenTenant_IgnoringAbsentHeader()
    {
        var tenant = Guid.NewGuid();
        var sut = new CurrentTenant(Accessor(AuthenticatedContext(tenant)));

        Assert.Equal(tenant, sut.Id);
    }

    [Fact]
    public void Id_WhenAuthenticatedAndHeaderMatchesToken_ReturnsTenant()
    {
        var tenant = Guid.NewGuid();
        var sut = new CurrentTenant(Accessor(AuthenticatedContext(tenant, tenant)));

        Assert.Equal(tenant, sut.Id);
    }

    [Fact]
    public void Id_WhenAuthenticatedAndHeaderDiffersFromToken_ThrowsTenantMismatch()
    {
        var sut = new CurrentTenant(Accessor(AuthenticatedContext(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Throws<TenantMismatchException>(() => sut.Id);
    }

    [Fact]
    public void Id_WhenUnauthenticated_ReturnsHeaderTenant()
    {
        var headerTenant = Guid.NewGuid();
        var ctx = new DefaultHttpContext();
        ctx.Items["TenantId"] = headerTenant;
        var sut = new CurrentTenant(Accessor(ctx));

        Assert.Equal(headerTenant, sut.Id);
    }

    [Fact]
    public void Id_WhenUnauthenticatedAndNoHeader_ReturnsEmpty()
    {
        var sut = new CurrentTenant(Accessor(new DefaultHttpContext()));

        Assert.Equal(Guid.Empty, sut.Id);
    }
}
