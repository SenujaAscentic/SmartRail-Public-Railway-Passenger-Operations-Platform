using System.Text;

namespace SmartRail.Network.Seed;

// Splits one CSV line into its columns. A column in double quotes may contain commas.
// stations.csv needs nothing more than this, so there is no CSV package.
public static class CsvLine
{
    public static IReadOnlyList<string> Split(string line)
    {
        var columns = new List<string>();
        var current = new StringBuilder();
        var insideQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                insideQuotes = !insideQuotes;   // the quote itself is not part of the value
            }
            else if (ch == ',' && !insideQuotes)
            {
                columns.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        columns.Add(current.ToString());
        return columns;
    }
}
