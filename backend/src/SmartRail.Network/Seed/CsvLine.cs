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

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"' && insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');   // two quotes inside a quoted field are one quote character
                i++;
            }
            else if (ch == '"')
            {
                insideQuotes = !insideQuotes;   // an opening or closing quote is not part of the value
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
