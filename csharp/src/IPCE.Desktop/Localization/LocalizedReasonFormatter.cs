namespace IPCE.Desktop.Localization;

public sealed class LocalizedReasonFormatter(
    ILocalizationService localization)
{
    private static readonly IReadOnlyDictionary<string, string> Keys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["硅测量输入已更新"] = "Reason.SiliconInputUpdated",
            ["积分使用的 IPCE 来源已改变"] = "Reason.IpceSourceChanged",
            ["计算 IPCE 已更新"] = "Reason.CalculatedIpceUpdated",
            ["太阳光谱已更新"] = "Reason.SpectrumUpdated",
            ["外部 IPCE 已更新"] = "Reason.ExternalIpceUpdated",
            ["相关输入已改变"] = "Reason.RelatedInputChanged",
            ["样品计算输入已更新"] = "Reason.SampleInputUpdated",
            ["样品暗电流扣除设置已改变"] = "Reason.SampleDarkSubtractionChanged",
            ["样品暗区起点已改变"] = "Reason.SampleDarkStartChanged",
            ["样品暗区终点已改变"] = "Reason.SampleDarkEndChanged",
            ["样品标称延时已改变"] = "Reason.SampleDelayChanged",
            ["样品波长步长已改变"] = "Reason.SampleStepChanged",
            ["样品固定起点已改变"] = "Reason.SampleFixedStartChanged",
            ["样品面积已改变"] = "Reason.SampleAreaChanged",
            ["样品平均时长已改变"] = "Reason.SampleAverageChanged",
            ["样品起始波长已改变"] = "Reason.SampleStartChanged",
            ["样品时间对齐方式已改变"] = "Reason.SampleAlignmentChanged",
            ["样品终止波长已改变"] = "Reason.SampleEndChanged",
            ["硅暗电流扣除设置已改变"] = "Reason.SiliconDarkSubtractionChanged",
            ["硅暗区起点已改变"] = "Reason.SiliconDarkStartChanged",
            ["硅暗区终点已改变"] = "Reason.SiliconDarkEndChanged",
            ["硅标称延时已改变"] = "Reason.SiliconDelayChanged",
            ["硅波长步长已改变"] = "Reason.SiliconStepChanged",
            ["硅固定起点已改变"] = "Reason.SiliconFixedStartChanged",
            ["硅面积已改变"] = "Reason.SiliconAreaChanged",
            ["硅平均时长已改变"] = "Reason.SiliconAverageChanged",
            ["硅起始波长已改变"] = "Reason.SiliconStartChanged",
            ["硅时间对齐方式已改变"] = "Reason.SiliconAlignmentChanged",
            ["硅终止波长已改变"] = "Reason.SiliconEndChanged",
            ["积分起点已改变"] = "Reason.IntegrationStartChanged",
            ["积分终点已改变"] = "Reason.IntegrationEndChanged",
        };
    private readonly ILocalizationService _localization = localization ??
        throw new ArgumentNullException(nameof(localization));

    public string Format(string reason) =>
        Keys.TryGetValue(reason, out string? key)
            ? _localization[key]
            : reason;
}
