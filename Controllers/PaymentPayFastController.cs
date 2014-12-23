using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using NopExtension.Plugins.PayFast.Models;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;

namespace NopExtension.Plugins.PayFast.Controllers
{
    public class PaymentPayFastController : BaseNopPaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly PayFastPaymentSettings _payFastPaymentSettings;
        private readonly PaymentSettings _paymentSettings;

        public PaymentPayFastController(ISettingService settingService,
            IPaymentService paymentService, IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            ILogger logger, IWebHelper webHelper,
            PayFastPaymentSettings payFastPaymentSettings,
            PaymentSettings paymentSettings)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._logger = logger;
            this._webHelper = webHelper;
            this._payFastPaymentSettings = payFastPaymentSettings;
            this._paymentSettings = paymentSettings;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new ConfigurationModel();
            model.UseSandbox = _payFastPaymentSettings.UseSandbox;
            model.IncludeNotifyUrl = _payFastPaymentSettings.IncludeNotifyUrl;
            model.LiveProcessorUrl = _payFastPaymentSettings.LiveProcessorUrl;
            model.LiveValidateUrl = _payFastPaymentSettings.LiveValidateUrl;
            model.MerchantId = _payFastPaymentSettings.MerchantId;
            model.MerchantKey = _payFastPaymentSettings.MerchantKey;
            model.SandboxProcessorUrl = _payFastPaymentSettings.SandboxProcessorUrl;
            model.SandboxValidateUrl = _payFastPaymentSettings.SandboxValidateUrl;

            model.AdditionalFee = _payFastPaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = _payFastPaymentSettings.AdditionalFeePercentage;

            return View("NopExtension.Plugins.PayFast.Views.PaymentPayFast.Configure", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _payFastPaymentSettings.UseSandbox = model.UseSandbox;
            _payFastPaymentSettings.IncludeNotifyUrl = model.IncludeNotifyUrl;
            _payFastPaymentSettings.LiveProcessorUrl = model.LiveProcessorUrl;
            _payFastPaymentSettings.LiveValidateUrl = model.LiveValidateUrl;
            _payFastPaymentSettings.MerchantId = model.MerchantId;
            _payFastPaymentSettings.MerchantKey = model.MerchantKey;
            _payFastPaymentSettings.SandboxProcessorUrl = model.SandboxProcessorUrl;
            _payFastPaymentSettings.SandboxValidateUrl = model.SandboxValidateUrl;
            _payFastPaymentSettings.AdditionalFee = model.AdditionalFee;
            _payFastPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            _settingService.SaveSetting(_payFastPaymentSettings);

            return View("NopExtension.Plugins.PayFast.Views.PaymentPayFast.Configure", model);
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("NopExtension.Plugins.PayFast.Views.PaymentPayFast.PaymentInfo");
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [ValidateInput(false)]
        public ActionResult PayFastNotify()
        {
            _logger.InsertLog(LogLevel.Information, "PayFast ITN Received");
            byte[] param = Request.BinaryRead(Request.ContentLength);
            string strRequest = Encoding.ASCII.GetString(param);
            Dictionary<string, string> values;

            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.PayFast") as PayFastPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("PayFast module cannot be loaded");
            _logger.InsertLog(LogLevel.Information, "Verifying Payfast ITN");
            if (processor.VerifyIPN(strRequest, out values))
            {
                _logger.InsertLog(LogLevel.Information, "PayFast Verifying ITN Verified");
                _logger.InsertLog(LogLevel.Information, "PayFast retrieving values");
                
                string paymentStatus;
                values.TryGetValue("payment_status", out paymentStatus);
                string pendingReason;
                values.TryGetValue("pending_reason", out pendingReason);
                string txnId;
                values.TryGetValue("pf_payment_id", out txnId);
                string txnType;
                values.TryGetValue("txn_type", out txnType);
                
                var sb = new StringBuilder();
                sb.AppendLine("PayFast IPN:");
                foreach (KeyValuePair<string, string> kvp in values)
                {
                    sb.AppendLine(kvp.Key + ": " + kvp.Value);
                    
                }
                _logger.InsertLog(LogLevel.Information, sb.ToString());
                var newPaymentStatus = PayFastHelper.GetPaymentStatus(paymentStatus, pendingReason);
                sb.AppendLine("New payment status: " + newPaymentStatus);

                switch (txnType)
                {
                    case "recurring_payment_profile_created":
                        //do nothing here
                        break;
                    case "recurring_payment":
                        //do nothing here
                        break;
                    default:
                        #region Standard payment
                        {
                            string orderNumber;
                            values.TryGetValue("m_payment_id", out orderNumber);
                            Guid orderNumberGuid = Guid.Empty;
                            try
                            {
                                orderNumberGuid = new Guid(orderNumber);
                            }
                            catch
                            {
                            }
                            _logger.InsertLog(LogLevel.Information, "Completing order: " + orderNumber);
                            var order = _orderService.GetOrderByGuid(orderNumberGuid);
                            if (order != null)
                            {

                                //order note
                                order.OrderNotes.Add(new OrderNote()
                                {
                                    Note = sb.ToString(),
                                    DisplayToCustomer = false,
                                    CreatedOnUtc = DateTime.UtcNow
                                });
                                _orderService.UpdateOrder(order);

                                switch (newPaymentStatus)
                                {
                                    case PaymentStatus.Pending:
                                        {
                                        }
                                        break;
                                    case PaymentStatus.Authorized:
                                        {
                                            if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                                            {
                                                _orderProcessingService.MarkAsAuthorized(order);
                                            }
                                        }
                                        break;
                                    case PaymentStatus.Paid:
                                        {
                                            _logger.InsertLog(LogLevel.Information, "Setting order as paid");
                                            if (_orderProcessingService.CanMarkOrderAsPaid(order))
                                            {

                                                order.AuthorizationTransactionId = txnId;
                                                _orderService.UpdateOrder(order);

                                                _orderProcessingService.MarkOrderAsPaid(order);
                                                _logger.InsertLog(LogLevel.Information, string.Format("Order: {0} paid", orderNumber));
                                            }
                                        }
                                        break;
                                    case PaymentStatus.Refunded:
                                        {
                                            if (_orderProcessingService.CanRefundOffline(order))
                                            {
                                                _orderProcessingService.RefundOffline(order);
                                            }
                                        }
                                        break;
                                    case PaymentStatus.Voided:
                                        {
                                            if (_orderProcessingService.CanVoidOffline(order))
                                            {
                                                _orderProcessingService.VoidOffline(order);
                                            }
                                        }
                                        break;
                                    default:
                                        _logger.InsertLog(LogLevel.Error, "No Payment status found");
                                        break;
                                }
                            }
                            else
                            {
                                _logger.Error("PayFast IPN. Order is not found", new NopException(sb.ToString()));
                            }
                        }
                        #endregion
                        break;
                }
            }
            else
            {
                _logger.Error("PayFast IPN failed.", new NopException(strRequest));
            }

            //nothing should be rendered to visitor
            return Content("");
        }

        [ChildActionOnly]
        public ActionResult CancelOrder(FormCollection form)
        {
            return View("NopExtension.Plugins.PayFast.Views.PaymentPayFast.PaymentCancel");
        }

        public ActionResult PayFastComplete()
        {
            
            return RedirectToAction("Orders", "Customer");
        }
    }
}