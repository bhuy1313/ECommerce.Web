namespace ECommerce.Web.Services.Email;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TModel>(string viewPath, TModel model);
}
