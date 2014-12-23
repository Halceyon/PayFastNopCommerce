using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using NopExtension.Plugins.PayFast.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using Nop.Services.Discounts;
using Nop.Services.Shipping;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Tax;

namespace NopExtension.Plugins.PayFast
{
    /// <summary>
    /// PayFast payment processor
    /// </summary>
    public class PayFastPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly PayFastPaymentSettings _payFastPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ITaxService _taxService;
        private readonly HttpContextBase _httpContext;
        private readonly PaymentSettings _paymentSettings;
        private readonly IPluginFinder _pluginFinder;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly IWorkContext _workContext;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IShippingService _shippingService;
        private readonly IDiscountService _discountService;
        private readonly IGiftCardService _giftCardService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly TaxSettings _taxSettings;
        private readonly RewardPointsSettings _rewardPointsSettings;
        private readonly ShippingSettings _shippingSettings;
        private readonly CatalogSettings _catalogSettings;
        private readonly IStoreContext _storeContext;
        #endregion

        #region Ctor

        public PayFastPaymentProcessor(PayFastPaymentSettings payFastPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            ICheckoutAttributeParser checkoutAttributeParser, 
            ITaxService taxService,
            ShoppingCartSettings shoppingCartSettings,
            HttpContextBase httpContext,
            IWorkContext workContext,
            PaymentSettings paymentSettings,
            IPluginFinder pluginFinder,
            IPriceCalculationService priceCalculationService,
            IShippingService shippingService,
            IDiscountService discountService,
            IGiftCardService giftCardService,
            IGenericAttributeService genericAttributeService,
            TaxSettings taxSettings,
            RewardPointsSettings rewardPointsSettings,
            ShippingSettings shippingSettings,
            CatalogSettings catalogSettings, IStoreContext storeContext)
        {
            _payFastPaymentSettings = payFastPaymentSettings;
            _settingService = settingService;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _webHelper = webHelper;
            _checkoutAttributeParser = checkoutAttributeParser;
            _taxService = taxService;
            _httpContext = httpContext;

            _workContext = workContext;
            _paymentSettings = paymentSettings;
            _pluginFinder = pluginFinder;
            _shoppingCartSettings = shoppingCartSettings;

            _priceCalculationService = priceCalculationService;
            _shippingService = shippingService;
            _discountService = discountService;
            _giftCardService = giftCardService;
            _genericAttributeService = genericAttributeService;
            _taxSettings = taxSettings;
            _rewardPointsSettings = rewardPointsSettings;
            _shippingSettings = shippingSettings;
            _catalogSettings = catalogSettings;
            _storeContext = storeContext;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets PayFast URL
        /// </summary>
        /// <returns></returns>
        private string GetPayFastUrl()
        {
            return _payFastPaymentSettings.UseSandbox ? _payFastPaymentSettings.SandboxProcessorUrl : _payFastPaymentSettings.LiveProcessorUrl;
        }

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <param name="values">Values</param>
        /// <returns>Result</returns>
        public bool VerifyIPN(string formString, out Dictionary<string, string> values)
        {
            // validate originator of request

            //bool validRequest = ValidateITNRequest();

            //if (!validRequest)
            //    throw new NopException("ITN Request came from an invalid source");

            // get posted variables.
            NameValueCollection formVariables = _httpContext.Request.Form;



            // authorize request
            bool validRequest = AuthorizeITNRequest(formVariables);

            if (!validRequest)
                throw new NopException("ITN Request is Unauthorized");

            // get order id (for debugging/logging)
            var orderId = formVariables["m_payment_id"];
            var processorOrderId = formVariables["pf_payment_id"];


            // the request is legitamite. Post back to the payment processor to validate the data received
            // Exclude the signature (it must be excluded when we hash and also when we validate).
            NameValueCollection formVariablesWithoutHash = new NameValueCollection();
            foreach (var item in formVariables.AllKeys.Where(x => x.ToLower() != "signature"))
                formVariablesWithoutHash.Add(item, formVariables[item]);

            //validRequest = ValidateITNRequestData(formVariablesWithoutHash);

            //if (!validRequest)
            //    throw new NopException(string.Format("PayFast ITN validation failed. Order id: {0}, PayFast Id {1}",
            //        orderId, processorOrderId));

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in formString.Split('&'))
            {
                string line = l.Trim();
                int equalPox = line.IndexOf('=');
                if (equalPox >= 0)
                    values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
            }

            return true;
        }


        #region PayFastMethods
        /// <summary>
        /// Validates that the request comes from the payment processor's servers
        /// </summary>
        public bool ValidateITNRequest()
        {
            bool isValid = true;
            string[] validSites = new string[] { 
                "www.payfast.co.za", 
                "sandbox.payfast.co.za", 
                "w1w.payfast.co.za", 
                "w2w.payfast.co.za" 
            };

            List<IPAddress> validIpAddresses = new List<IPAddress>();
            foreach (var url in validSites)
                validIpAddresses.AddRange(Dns.GetHostAddresses(url));

            string requestIp = _webHelper.ServerVariables("REMOTE_ADDR");
            if (string.IsNullOrEmpty(requestIp))
            {
                throw new NopException("PayFast ITN Request is invalid", "The source IP address of the ITN request was null");
            }


            if (isValid)
                if (!validIpAddresses.Contains(IPAddress.Parse(requestIp)))
                {
                    throw new NopException(string.Format("The source IP address of the ITN request ({0}) is not valid", requestIp));
                }

            return isValid;
        }

        /// <summary>
        /// Authorizes the request to ensure the correct merchant id is included along with a valid ITN signature
        /// </summary>
        public bool AuthorizeITNRequest(NameValueCollection formVariables)
        {
            bool isValid = true;

            // verify that we are the intended merchant
            string receivedMerchant = formVariables["merchant_id"];

            if (receivedMerchant.ToLower() != _payFastPaymentSettings.MerchantId.ToLower())
            {
                isValid = false;
                //LogManager.InsertLog(LogTypeEnum.OrderError,
                //    "PayFast ITN Request is invalid", "The merchant id of the request does not match the site's PayFast merchant id");
            }

            if (isValid)
            {
                string postedSignature = formVariables["signature"];
                if (string.IsNullOrEmpty(postedSignature))
                {
                    isValid = false;
                    //LogManager.InsertLog(LogTypeEnum.OrderError,
                    //    "PayFast ITN Request is invalid", "The PayFast ITN Signature parameter cannot be null.");
                }
            }

            return isValid;
        }

        /// <summary>
        /// Posts the data back to the payment processor to validate the data received
        /// </summary>
        public bool ValidateITNRequestData(NameValueCollection formVariables)
        {
            bool isValid = true;
            try
            {
                StringBuilder sb = new StringBuilder();
                bool first = true;
                foreach (var item in formVariables)
                {
                    if (first) first = false;
                    else sb.Append("&");
                    sb.AppendFormat("{0}={1}", item.ToString(), HttpUtility.UrlEncode(formVariables[item.ToString()]));
                }
                byte[] postBytes = Encoding.ASCII.GetBytes(sb.ToString());

                string validateUrl = (_payFastPaymentSettings.UseSandbox) ? _payFastPaymentSettings.SandboxValidateUrl : _payFastPaymentSettings.LiveValidateUrl;
                HttpWebRequest req = HttpWebRequest.Create(validateUrl) as HttpWebRequest;
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = postBytes.Length;

                // add post data to request
                using (var postStream = req.GetRequestStream())
                {
                    postStream.Write(postBytes, 0, postBytes.Length);
                }

                string result = null;
                using (var responseStream = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    result = HttpUtility.UrlDecode(responseStream.ReadToEnd());
                }

                result = result.Replace("\r\n", " ").Replace("\r", "").Replace("\n", " ");
                if (result == null || !result.StartsWith("VALID", StringComparison.OrdinalIgnoreCase))
                {
                    isValid = false;
                    //LogManager.InsertLog(LogTypeEnum.OrderError, "PayFast ITN validation failed",
                    //    "The validation response was not valid.");
                }

            }
            catch (Exception ex)
            {

                //LogManager.InsertLog(LogTypeEnum.Unknown, "Unable to validate ITN data. Unknown exception", ex);
                isValid = false;
            }
            return isValid;
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            // validate originator of request


            var returnUrl = _webHelper.GetStoreLocation(false) + "Plugins/PaymentPayFast/PayFastComplete";
            var cancelUrl = _webHelper.GetStoreLocation(false) + "Customer/Orders";
            var notifyUrl = _webHelper.GetStoreLocation(false) + "Plugins/PaymentPayFast/PayFastNotify";
            StringBuilder sb = new StringBuilder();
            string paramFormat = "{0}={1}";

            sb.AppendFormat(paramFormat, "merchant_id", HttpUtility.UrlEncode(_payFastPaymentSettings.MerchantId));
            sb.AppendFormat(paramFormat, "&merchant_key", HttpUtility.UrlEncode(_payFastPaymentSettings.MerchantKey));
            sb.AppendFormat(paramFormat, "&return_url", HttpUtility.UrlEncode(returnUrl));
            sb.AppendFormat(paramFormat, "&cancel_url", HttpUtility.UrlEncode(cancelUrl));
            if (_payFastPaymentSettings.IncludeNotifyUrl)
                sb.AppendFormat(paramFormat, "&notify_url", HttpUtility.UrlEncode(notifyUrl));
            sb.AppendFormat(paramFormat, "&m_payment_id", HttpUtility.UrlEncode(postProcessPaymentRequest.Order.OrderGuid.ToString()));
            sb.AppendFormat(paramFormat, "&amount", postProcessPaymentRequest.Order.OrderTotal);

            sb.AppendFormat(paramFormat, "&item_name", HttpUtility.UrlEncode("Purchase from Store"));
            sb.AppendFormat(paramFormat, "&name_first", postProcessPaymentRequest.Order.Customer.BillingAddress.FirstName);
            sb.AppendFormat(paramFormat, "&name_last", postProcessPaymentRequest.Order.Customer.BillingAddress.LastName);
            sb.AppendFormat(paramFormat, "&email_address", postProcessPaymentRequest.Order.Customer.Email);
            var processorUrl = GetPayFastUrl();
            HttpContext.Current.Response.Redirect(processorUrl + sb.ToString());
            //return string.Empty;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            //return _payFastPaymentSettings.AdditionalFee;
            var percentagefee = decimal.Zero;
            if (cart.Count > 0)
            {
                var paymentService = new PaymentServiceHook(_paymentSettings, _pluginFinder, _shoppingCartSettings);

                var orderTotalCalculationService = new OrderTotalCalculationService(_workContext,_storeContext, _priceCalculationService,
                                                       _taxService, _shippingService,
                                                       paymentService,
                                                       _checkoutAttributeParser,
                                                       _discountService,
                                                       _giftCardService,
                                                       _genericAttributeService,
                                                       _taxSettings,
                                                       _rewardPointsSettings,
                                                       _shippingSettings,
                                                       _shoppingCartSettings,
                                                       _catalogSettings);

                var orderTotal = orderTotalCalculationService.GetShoppingCartTotal(cart);
                
                // ToDo: Additional Fee with Tax, Ignore Rewards Points? 
                if (orderTotal != null)
                {
                    percentagefee = (100 / (100 - _payFastPaymentSettings.AdditionalFeePercentage) - 1) * (decimal)orderTotal;
                }
            }

            return _payFastPaymentSettings.AdditionalFee + percentagefee;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //PayPal Standard is the redirection payment method
            //It also validates whether order is also paid (after redirection) so customers will not be able to pay twice

            //payment status should be Pending
            if (order.PaymentStatus != PaymentStatus.Pending)
                return false;

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPayFast";
            routeValues = new RouteValueDictionary() { { "Namespaces", "NopExtension.Plugins.PayFast.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPayFast";
            routeValues = new RouteValueDictionary() { { "Namespaces", "NopExtension.Plugins.PayFast.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentPayFastController);
        }

        public override void Install()
        {
            //settings
            var settings = new PayFastPaymentSettings()
            {
                UseSandbox = true,
                MerchantId = "10000100",
                MerchantKey = "46f0cd694581a",
                IncludeNotifyUrl = true,
                SandboxProcessorUrl = "https://sandbox.payfast.co.za/eng/process?",
                SandboxValidateUrl = "https://sandbox.payfast.co.za/eng/validate?",
                LiveProcessorUrl = "https://www.payfast.co.za/eng/process?",
                LiveValidateUrl = "https://www.payfast.co.za/eng/validate?",
                AdditionalFee = Convert.ToDecimal(2),
                AdditionalFeePercentage = Convert.ToDecimal(4.9)
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.RedirectionTip", "You will be redirected to PayFast site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.MerchantId", "Merchant Id");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.MerchantId.Hint", "Specify your PayFast Merchant Id.");

            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.MerchantKey", "Merchant Key");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.MerchantKey.Hint", "Specify your PayFast Merchant Key.");

            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.IncludeNotifyUrl", "Include Notify Url");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.IncludeNotifyUrl.Hint", "");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxProcessorUrl", "Sandbox Processor Url");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxProcessorUrl.Hint", "");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveProcessorUrl", "Live Processor Url");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveProcessorUrl.Hint", "");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxValidateUrl", "Sandbox Validate Url");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxValidateUrl.Hint", "");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveValidateUrl", "Live Validate Url");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveValidateUrl.Hint", "");

            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFee", "Additional Fixed Fee");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFee.Hint", "");

            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFeePercentage", "Additional Fee %");
            this.AddOrUpdatePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFeePercentage.Hint", "");

            base.Install();
        }

        public override void Uninstall()
        {
            //locales
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.UseSandbox");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.MerchantId");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.MerchantId.Hint");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.IncludeNotifyUrl");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.IncludeNotifyUrl)");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxProcessorUrl");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxProcessorUrl.Hint");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveProcessorUrl");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveProcessorUrl.Hint");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxValidateUrl");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.SandboxValidateUrl.Hint");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveValidateUrl");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.LiveValidateUrl.Hint");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("NopExtension.Plugins.PayFast.Fields.AdditionalFeePercentage.Hint");
            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        #endregion
    }
}
