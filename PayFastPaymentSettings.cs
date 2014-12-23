using Nop.Core.Configuration;

namespace NopExtension.Plugins.PayFast
{
    public class PayFastPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public string MerchantId { get; set; }
        public string MerchantKey { get; set; }
        public bool IncludeNotifyUrl { get; set; }
        public string SandboxProcessorUrl { get; set; }
        public string LiveProcessorUrl { get; set; }
        public string SandboxValidateUrl { get; set; }
        public string LiveValidateUrl { get; set; }
        public decimal PdtValidateOrderTotal { get; set; }
        public decimal AdditionalFee { get; set; }
        public decimal AdditionalFeePercentage { get; set; }
    }
}
