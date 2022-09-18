namespace Qaplix.Val;

internal class Election2018
{
    public Election2018()
    {
        _map18to22 = new Lazy<Dictionary<string, IList<string>>>(LoadMappings);
        _votes = new Lazy<Dictionary<string, Dictionary<string, int>>>(LoadVotes);
    }

    private Lazy<Dictionary<string, Dictionary<string, int>>> _votes;
    private Lazy<Dictionary<string, IList<string>>> _map18to22;
    public Dictionary<string, IList<string>> Map18to22 => _map18to22.Value;
    public Dictionary<string, Dictionary<string, int>> Votes => _votes.Value;

    public Dictionary<string, IList<string>> LoadMappings()
    {
        return File.ReadAllLines(@"data/uppsamlingsdistrikt-jamforelse-2018.csv")
            .Select(x => x.Split(","))
            .Skip(1)
            .ToDictionary(x => x[0], x => new string[] { x[3], x[4], x[5] }.Where(x => !string.IsNullOrEmpty(x)).ToList() as IList<string>);
    }

    public Dictionary<string, Dictionary<string, int>> LoadVotes()
    {
        var lines = File.ReadAllLines(@"data/valresultat-2018.csv");
        var koder = lines[0].Split(",").Skip(8).Take(8).ToArray();
        return lines
            .Skip(1)
            .Where(x => x.Contains("ppsamlingsdistrikt"))
            .Select(x => x.Split(","))
            .Where(x => int.Parse(x[3]) < 10)
            .ToDictionary(
                x => $"{int.Parse(x[0]):00}{int.Parse(x[1]):00}{int.Parse(x[3]):00}",
                x => x.Skip(8).Take(8).Zip(koder).ToDictionary(x => x.Second, x => int.TryParse(x.First, out var i)?i:0));
                    
    }
}
