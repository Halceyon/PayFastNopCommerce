using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace NopExtension.Plugins.PayFast.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.MerchantId")]
        public string MerchantId { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.MerchantKey")]
        public string MerchantKey { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.IncludeNotifyUrl")]
        public bool IncludeNotifyUrl { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.SandboxProcessorUrl")]
        public string SandboxProcessorUrl { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.LiveProcessorUrl")]
        public string LiveProcessorUrl { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.SandboxValidateUrl")]
        public string SandboxValidateUrl { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.LiveValidateUrl")]
        public string LiveValidateUrl { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }

        [NopResourceDisplayName("NopExtension.Plugins.PayFast.Fields.AdditionalFeePercentage")]
        public decimal AdditionalFeePercentage { get; set; }
    }
}