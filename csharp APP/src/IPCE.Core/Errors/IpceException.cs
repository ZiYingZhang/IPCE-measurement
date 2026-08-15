namespace IPCE.Core.Errors;

public sealed class IpceException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}
