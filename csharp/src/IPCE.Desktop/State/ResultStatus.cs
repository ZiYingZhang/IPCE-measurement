namespace IPCE.Desktop.State;

public enum ResultFreshness
{
    Missing,
    Current,
    Stale,
}

public sealed record ResultStatus(
    ResultFreshness Freshness,
    string Reason)
{
    public bool CanUse => Freshness == ResultFreshness.Current;
}
