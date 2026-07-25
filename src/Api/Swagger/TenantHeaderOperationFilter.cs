using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace SchoolMaster.Api.Swagger;
// IOperationFilter allows us to modify the generated Swagger documentation for each API operation. In this case, we are adding a custom header parameter to all API endpoints to indicate that they can accept the "X-Tenant-Subdomain" header for tenant resolution.
public class TenantHeaderOperationFilter : IOperationFilter
{
    // operation represents the specific API endpoint being documented, and context provides additional information about the endpoint.
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // if the parameter folder is null, create a new holder to hold the parameters, otherwise add to the existing holder
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-Subdomain",
            In = ParameterLocation.Header,
            Description = "Tenant subdomain (e.g., susie.academy.edu). Required for unauthenticated endpoints.",
            Required = false, // Set to false so it doesn't block authenticated endpoints that use JWT tenant_id
            Schema = new OpenApiSchema
            {
                Type = "string"
            }
        });
    }
}
