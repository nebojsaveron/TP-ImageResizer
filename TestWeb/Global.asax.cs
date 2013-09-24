using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

namespace TestWeb
{
    public class MvcApplication : HttpApplication
    {
        private readonly Regex _regex = new Regex(SiteConfig.Instance.UserImagesRelativePath + @"([A-Za-z0-9]+)/((original).([a-z]+)$|(cropped-)?(w([0-9]+))?-?(h([0-9]+))?.([a-z]+))$");

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            ImageConfig.ConfigureImageResizer(_regex);
        }
    }
}