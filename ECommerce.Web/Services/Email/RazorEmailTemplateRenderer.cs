using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Web.Services.Email;

public sealed class RazorEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IModelMetadataProvider _modelMetadataProvider;

    public RazorEmailTemplateRenderer(
        IHttpContextAccessor httpContextAccessor,
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IModelMetadataProvider modelMetadataProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _modelMetadataProvider = modelMetadataProvider;
    }

    public async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Email templates require an active HTTP request.");

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: false);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException(
                $"Email view '{viewPath}' not found. Searched: " +
                $"{string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");
        }

        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(_modelMetadataProvider, new ModelStateDictionary())
        {
            Model = model
        };
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            new TempDataDictionary(httpContext, _tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
