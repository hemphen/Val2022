using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qaplix.Val;

record PartyData(string Code, string Abbreviation, string Name);
record CodeName(string Code, string Name);

public class RegionLevel
{
    public RegionLevel(int level, Func<Valdistrikt, string> grouping, Dictionary<string, string> names)
    {
        Level = level;
        Names = names;
        Grouping = grouping;
    }

    public int Level { get; }
    public Dictionary<string, string> Names { get; }
    public Func<Valdistrikt, string> Grouping { get; }
}

class ElectionMetaDataLoader
{
    private Lazy<Dictionary<string, ElectionMetaData>> _elections;
    public Dictionary<string, ElectionMetaData> Elections => _elections.Value;

    private Lazy<Dictionary<string, MapData>> _maps;
    public Dictionary<string, MapData> Maps => _maps.Value;

    public ElectionMetaDataLoader()
    {
        _maps = new Lazy<Dictionary<string, MapData>>(LoadMaps);
        _elections = new Lazy<Dictionary<string, ElectionMetaData>>(LoadElections);
    }

    private Dictionary<string, ElectionMetaData> LoadElections()
    {
        var lines = File
            .ReadAllLines(@"data/deltagande-partier.csv")
            .Skip(1)
            .Select(x => x.Split(";"))
            .ToList();

        var parties = lines
            .GroupBy(x => x[9]) // Partikod
                .Select(x => x.OrderByDescending(x => DateTime.Parse(x[11])).First())
                .Select(x => new PartyData(x[9], x[8], x[7]))
                .ToDictionary(x => x.Code, x => x);

        var kommuner = lines
            .Where(x => x[0] == "KF")
            .Select(x => new CodeName(x[1], x[2]))
            .Distinct()
            .ToDictionary(x => x.Code, x => x.Name);

        return lines
            .GroupBy(l => l[0])
            .ToDictionary(
                l => l.Key,
                l => new ElectionMetaData(
                    parties,
                    new List<RegionLevel> {
                        new (
                            0,
                            x=>x.valomradeskod,
                            l.Select(x => new CodeName(x[1], x[2]))
                                .Distinct()
                                .ToDictionary(c => c.Code, c => c.Name)),
                        new (
                            1,
                            x=>x.lankod,
                            l.Select(x => new CodeName(x[5], x[6]))
                                .Distinct()
                                .ToDictionary(c => c.Code, c => c.Name)),
                        new (
                            2,
                            x=>x.kretskod,
                            l.Select(x => new CodeName(x[3], x[4]))
                                .Distinct()
                                .ToDictionary(c => c.Code, c => c.Name)),
                        new(
                            3,
                            x => x.kommunkod,
                            kommuner) }));
    }

    private Dictionary<string, MapData> LoadMaps()
    {
        return Directory.GetFiles("valdistrikt", "*.zip")
            .Select(async file =>
            {
                var stream = File.OpenRead(file);
                ZipArchive zip;
                try
                {
                    zip = new ZipArchive(stream);
                }
                catch
                {
                    throw;
                }

                if (zip.Entries.Count != 1) throw new InvalidDataException("More than one file in archive");
                var entry = zip.Entries.First();
                var regionMatch = Regex.Match(entry.Name, @"VD_(?<code>\d+)");
                var regionCode = regionMatch.Groups["code"].Value;
                var zipStream = entry.Open();
                var mapData = await JsonSerializer.DeserializeAsync<MapData>(zipStream);
                return (Region: regionCode, Data: mapData ?? new());
            })
            .ToDictionary(x => x.Result.Region, x => x.Result.Data);
    }
}

class ElectionMetaData
{
    public ElectionMetaData(Dictionary<string, PartyData> parties, List<RegionLevel> levels)
    {
        Levels = levels;
        Parties = parties;
    }

    public List<RegionLevel> Levels { get; }
    public Dictionary<string, PartyData> Parties { get; }


    public string Abbreviate(string partyCode) => Parties.TryGetValue(partyCode, out var partyData) ? partyData.Abbreviation : "??";
    public string GetPartyCode(string abbreviation) => Parties.Values.First(x => x.Abbreviation == abbreviation).Code;
}