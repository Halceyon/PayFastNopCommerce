using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Services.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NopExtension.Plugins.PayFast
{
    public class PaymentServiceHook : PaymentService
    {
        public PaymentServiceHook(PaymentSettings paymentSettings, IPluginFinder pluginFinder, ShoppingCartSettings shoppingCartSettings) :
            base(paymentSettings, pluginFinder, shoppingCartSettings)
        {
        }

        //public override decimal GetAdditionalHandlingFee(string paymentMethodSystemName)
        //{
        //    return 0M;
        //}
    }

}
