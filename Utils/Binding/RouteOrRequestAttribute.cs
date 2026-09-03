using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Luxira.Api.Utils.Binding;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RouteOrRequestAttribute : ModelBinderAttribute
{
    public RouteOrRequestAttribute() : base(typeof(RouteOrRequestModelBinder)) { }
}

public sealed class RouteOrRequestModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var context = bindingContext;
        var name = context.FieldName;
        string? value = context.HttpContext.Request.RouteValues.TryGetValue(name, out var routeValue)
            ? Convert.ToString(routeValue, CultureInfo.InvariantCulture)
            : context.HttpContext.Request.Query[name].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value) && context.HttpContext.Request.HasFormContentType)
            value = (await context.HttpContext.Request.ReadFormAsync(context.HttpContext.RequestAborted))[name].FirstOrDefault();

        var targetType = Nullable.GetUnderlyingType(context.ModelType) ?? context.ModelType;
        if (string.IsNullOrWhiteSpace(value))
        {
            context.Result = Nullable.GetUnderlyingType(context.ModelType) is not null || targetType == typeof(string)
                ? ModelBindingResult.Success(null)
                : ModelBindingResult.Failed();
            return;
        }
        try
        {
            var converted = targetType == typeof(string) ? value : TypeDescriptor.GetConverter(targetType)
                .ConvertFrom(null, CultureInfo.InvariantCulture, value);
            context.Result = ModelBindingResult.Success(converted);
        }
        catch (Exception)
        {
            context.ModelState.TryAddModelError(name, $"Invalid value for {name}.");
            context.Result = ModelBindingResult.Failed();
        }
    }
}
