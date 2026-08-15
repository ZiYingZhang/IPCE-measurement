using System.Globalization;
using IPCE.IO.Tables;

namespace IPCE.IO.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DelimitedTableReaderTests
{
    [TestMethod]
    public void CommaSeparatedHeaderAndNumericRows_ArePreserved()
    {
        using var file = new TemporaryTextFile(
            "Wavelength/nm,IPCE/%\n400,50\n500,80\n",
            ".csv");

        TabularData table = DelimitedTableReader.Read(file.Path);

        CollectionAssert.AreEqual(
            new[] { "Wavelength/nm", "IPCE/%" },
            table.Headers.ToArray());
        Assert.AreEqual(2, table.NumericRows.Count);
        CollectionAssert.AreEqual(
            new[] { 400d, 50d },
            table.NumericRows[0].ToArray());
    }

    [TestMethod]
    public void TabSeparatedThousands_AreNotSplitAtComma()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            using var file = new TemporaryTextFile(
                "Time/s\tCurrent/A\n1,234\t5,678\n2,345\t6,789\n");

            TabularData table = DelimitedTableReader.Read(file.Path);

            CollectionAssert.AreEqual(
                new[] { 1234d, 5678d },
                table.NumericRows[0].ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void SemicolonSeparatedCurrentCultureDecimals_AreParsed()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            using var file = new TemporaryTextFile(
                "Time/s;Current/A\n0,5;1,5\n1,5;2,5\n");

            TabularData table = DelimitedTableReader.Read(file.Path);

            CollectionAssert.AreEqual(
                new[] { 0.5, 1.5 },
                table.NumericRows[0].ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
