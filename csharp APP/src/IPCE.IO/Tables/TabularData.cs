namespace IPCE.IO.Tables;

public sealed record TabularData
{
    public TabularData(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<double>> numericRows,
        string rawHeaderText)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(numericRows);

        Headers = Array.AsReadOnly(headers.ToArray());
        NumericRows = Array.AsReadOnly(numericRows
            .Select(row =>
                (IReadOnlyList<double>)Array.AsReadOnly(row.ToArray()))
            .ToArray());
        RawHeaderText = rawHeaderText ?? "";
    }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<IReadOnlyList<double>> NumericRows { get; }

    public string RawHeaderText { get; }
}
