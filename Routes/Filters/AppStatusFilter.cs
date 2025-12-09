using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HeatHarmony.Routes.Filters
{
    public class AppStatusFilter : IOperationFilter
    {
        void IOperationFilter.Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var isAppStatusEndpoint = context.ApiDescription.ActionDescriptor?.EndpointMetadata.OfType<TagsAttribute>().Any(t => t.Tags.Contains("AppStatusEndpoints"));

            if (isAppStatusEndpoint == true)
            {
                operation.Security = [];
            }
        }
    }
}
