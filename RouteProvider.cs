using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace NopExtension.Plugins.PayFast
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Payments.PayFast.Configure",
                 "Plugins/PaymentPayFast/Configure",
                 new { controller = "PaymentPayFast", action = "Configure" },
                 new[] { "NopExtension.Plugins.PayFast.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.PayFast.PaymentInfo",
                 "Plugins/PaymentPayFast/PaymentInfo",
                 new { controller = "PaymentPayFast", action = "PaymentInfo" },
                 new[] { "NopExtension.Plugins.PayFast.Controllers" }
            );

            //ITN
            routes.MapRoute("Plugin.Payments.PayFast.PayFastNotify",
                 "Plugins/PaymentPayFast/PayFastNotify",
                 new { controller = "PaymentPayFast", action = "PayFastNotify" },
                 new[] { "NopExtension.Plugins.PayFast.Controllers" }
            );
            //ITN
            routes.MapRoute("Plugin.Payments.PayFast.PayFastComplete",
                 "Plugins/PaymentPayFast/PayFastComplete",
                 new { controller = "PaymentPayFast", action = "PayFastComplete" },
                 new[] { "NopExtension.Plugins.PayFast.Controllers" }
            );
            //Cancel
            routes.MapRoute("Plugin.Payments.PayFast.CancelOrder",
                 "Plugins/PaymentPayFast/CancelOrder",
                 new { controller = "PaymentPayFast", action = "CancelOrder" },
                 new[] { "NopExtension.Plugins.PayFast.Controllers" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
