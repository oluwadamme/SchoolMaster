using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SchoolMaster.Application.Repositories;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace SchoolMaster.Api.Middlewares;

public class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolverMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // Scoped services (like ITenantRepository) must be injected into InvokeAsync, not the constructor.
    public async Task InvokeAsync(HttpContext context, ITenantRepository tenantRepository)
    {
        // 1. Extract subdomain from custom header
        if (context.Request.Headers.TryGetValue("X-Tenant-Subdomain", out var subdomainValues))
        {
            var subdomain = subdomainValues.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(subdomain))
            {
                // 2. Lookup the tenant
                var tenant = await tenantRepository.GetTenantBySubdomainAsync(subdomain);

                if (tenant != null)
                {
                    // 3. Store the TenantId in the request scope
                    context.Items["TenantId"] = tenant.Id;
                    Log.Information("Resolved TenantId {TenantId} for subdomain {Subdomain}", tenant.Id, subdomain);
                }
                else
                {
                    Log.Warning("Subdomain {Subdomain} provided but tenant not found.", subdomain);
                }
            }
        }

        // 4. Continue the request pipeline
        await _next(context);
    }
}