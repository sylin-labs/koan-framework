using Microsoft.OpenApi.Models;

namespace Koan.Web.Swagger;

internal sealed class TransformerMediaTypesOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    private readonly IServiceProvider _services;
    public TransformerMediaTypesOperationFilter(IServiceProvider services) { _services = services; }
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        var attrType = Type.GetType("Koan.Web.Transformers.EnableEntityTransformersAttribute, Koan.Web.Transformers");
        var registryType = Type.GetType("Koan.Web.Transformers.ITransformerRegistry, Koan.Web.Transformers");
        if (attrType is null || registryType is null) return;
        var hasAttr = context.MethodInfo.DeclaringType?.GetCustomAttributes(attrType, inherit: true).Any() == true;
        if (!hasAttr) return;

        var registry = _services.GetService(registryType);
        if (registry is null) return;

        Type? entityType = TryGetEntityType(context.MethodInfo.ReturnType);
        if (entityType is not null)
        {
            var getMi = registryType.GetMethod("GetContentTypes")!.MakeGenericMethod(entityType);
            var list = (IReadOnlyList<string>?)getMi.Invoke(registry, Array.Empty<object>());
            if (list is not null && list.Count > 0 && operation.Responses.TryGetValue("200", out var ok))
            {
                foreach (var ct in list)
                    if (!ok.Content.ContainsKey(ct)) ok.Content[ct] = new OpenApiMediaType();
            }
        }

        foreach (var p in context.MethodInfo.GetParameters())
        {
            var et = TryGetEntityType(p.ParameterType);
            if (et is null) continue;
            var getMi = registryType.GetMethod("GetContentTypes")!.MakeGenericMethod(et);
            var list = (IReadOnlyList<string>?)getMi.Invoke(registry, Array.Empty<object>());
            if (list is null || list.Count == 0) continue;
            operation.RequestBody ??= new OpenApiRequestBody { Required = true };
            foreach (var ct in list)
                if (!operation.RequestBody.Content.ContainsKey(ct))
                    operation.RequestBody.Content[ct] = new OpenApiMediaType();
        }
    }

    private static Type? TryGetEntityType(Type t)
    {
        if (t == typeof(void) || t == typeof(string)) return null;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Microsoft.AspNetCore.Mvc.ActionResult<>))
            t = t.GetGenericArguments()[0];
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            t = t.GetGenericArguments()[0];
        // implements IEntity<>
        var ok = t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == "Koan.Data.Abstractions.IEntity`1");
        return ok ? t : null;
    }
}