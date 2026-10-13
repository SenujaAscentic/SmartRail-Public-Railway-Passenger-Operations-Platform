using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.UnitTests.Network.Seed;

public class CsvLineTests
{
    [Fact]
    public void Quoted_field_containing_a_comma_stays_one_column()
    {
        var columns = CsvLine.Split("11,KDT,Kandy,\"Senuja (coords, 12 Oct)\"");

        Assert.Equal(["11", "KDT", "Kandy", "Senuja (coords, 12 Oct)"], columns);
    }

    [Fact]
    public void Empty_field_is_kept_as_an_empty_column()
    {
        Assert.Equal(["a", "", "c"], CsvLine.Split("a,,c"));
    }

    [Fact]
    public void Field_with_an_escaped_quote_keeps_one_quote_character()
    {
        // In CSV a quote inside a quoted field is written twice: "say ""hi""" means: say "hi"
        var columns = CsvLine.Split("a,\"say \"\"hi\"\", ok\",c");

        Assert.Equal(["a", "say \"hi\", ok", "c"], columns);
    }
}
