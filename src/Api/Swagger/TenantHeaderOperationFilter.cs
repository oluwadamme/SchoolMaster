using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace SchoolMaster.Api.Swagger;

public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
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
