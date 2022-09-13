using CommandLine;
using System.Data;

namespace Qaplix.Val;

public static class Program
{
    public class Options
    {
        [Option('w', "wednesday", Default = false, Required = false, HelpText = "Use wednesday votes from 2018 for uppsamlingsdistrikt.")]
        public bool UseWednesdayVotes { get; set; }
        [Value(0, Default = "preliminär", Required = false, HelpText = "Use a specific räkningstillfälle, e.g. preliminär/slutlig.")]
        public string? VoteCountOccasion { get; set; }
        [Option('f', "follow", Default = false, Required = false, HelpText = "Follow changes over time by re-running the analysis.")]
        public bool DoFollow { get; set; }
        [Option('d', "delay", Default = 30, Required = false, HelpText = "Delay between re-runs in seconds.")]
        public int Delay { get; set; }
        [Option('u', "url", Default = "https://resultat.val.se/resultatfiler/", Required = false, HelpText = "Base URL for result files.")]
        public string? Uri { get; set; }
        [Option('p', "path", Default = "results", Required = false, HelpText = "Path to result files.")]
        public string? BasePath { get; set; }
    }

    public static async Task Main(string[] args)
    {
        var options = await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(Run);
    }

    public static async Task Run(Options options)
    {
        var metaData = new ElectionMetaData();
        do
        {
            (var voteMap, var seatMap) = await FetchAndProcess(new Uri(options.Uri!), options.BasePath!, options.VoteCountOccasion!);
            AnalyzeAndPrint(metaData, voteMap, options.UseWednesdayVotes);
            if (options.DoFollow)
                await Task.Delay(options.Delay*1000);
        } while (options.DoFollow);
    }

    private static void AnalyzeAndPrint(ElectionMetaData metaData, Dictionary<string, RostData> voteMap, bool useWednesdayVotes)
    {
        var stateVotes = voteMap.Values.Where(x => x.valtyp == "RD");
        try
        {
            var allDistricts = stateVotes.SelectMany(x => x.valdistrikt);
            var votesData = CalcVotes(allDistricts);

            if (useWednesdayVotes)
            {
                votesData = AdjustForOnsdagsVotes2018(votesData);
            }

            var popularParties = votesData
                .PartyVotes
                .OrderByDescending(x => x.Value)
                .Take(10)
                .Select(x => metaData.Parties[x.Key])
                .ToList();

            Console.WriteLine("\nVotes\n");
            AnalyzeAndPrintVotes(metaData, allDistricts, votesData, popularParties);

            Console.WriteLine("\nSeats\n");
            AnalyzeAndPrintMandates(metaData, votesData, popularParties);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not list votesData data: {ex.Message}");
        }

    }

    private static void AnalyzeAndPrintVotes(ElectionMetaData metaData, IEnumerable<Valdistrikt> districts, VotesData votesData, List<PartyData> parties)
    {
        foreach (var party in votesData.PartyVotes.Select(x => (Info: metaData.Parties[x.Key], Votes: x.Value)))
        {
            Console.WriteLine($"{party.Info.Abbreviation,4} {party.Votes,8} {party.Info.Name}");
        }

        PrintHeader(parties);

        foreach (var region in districts.GroupBy(x => x.lankod))
        {
            PrintCount(CalcVotes(region), parties, metaData.Districts[region.Key]);
        }

        PrintCount(votesData, parties, "TOTALT");
    }

    private static void AnalyzeAndPrintMandates(ElectionMetaData metaData, VotesData votesData, IEnumerable<PartyData> parties)
    {
        double DelningsTal(int numSeats) => numSeats == 0 ? 1.2 : numSeats * 2 + 1;

        var eligibleParties = votesData.PartyVotes.Where(x => x.Value / (double)votesData.TotalVotes > 0.04);
        var seats = eligibleParties.ToDictionary(x => x.Key, x => 0);
        for (int i = 0; i < 349; i++)
        {
            var nextSeat = eligibleParties.Select(x => (PartyCode: x.Key, Value: x.Value / DelningsTal(seats[x.Key]))).OrderByDescending(x => x.Value).ToList();
            if (i > 340)
            {
                Console.WriteLine($"{metaData.Abbreviate(nextSeat[0].PartyCode),2} före {metaData.Abbreviate(nextSeat[1].PartyCode),2}: {nextSeat[0].Value:0.0} vs. {nextSeat[1].Value:0.0}");
            }
            seats[nextSeat[0].PartyCode] = seats[nextSeat[0].PartyCode] + 1;
        }

        var jamforelseTal = eligibleParties
            .Select(x => (PartyCode: x.Key, Value: x.Value / DelningsTal(seats[x.Key])))
            .OrderByDescending(x => x.Value)
            .Select(x => $"{metaData.Abbreviate(x.PartyCode),2}: {x.Value:0.0}");
        Console.WriteLine($"        {string.Join(", ", jamforelseTal)}");

        PrintHeader(parties);

        Console.Write($"{"MANDAT",20}");
        foreach (var party in parties)
        {
            Console.Write($"|{(seats.TryGetValue(party.Code, out var mandat) ? mandat : 0),7} ");
        }
        Console.WriteLine("|");

        var blocks = new List<(string Block, string[] Abbreviations)> {
                ("Vänstern      ", new[] { "S", "C", "V", "MP" }),
                ("Högern        ", new[] { "M", "L", "KD", "SD" }),
                ("Alliansen     ", new[] { "M", "L", "KD", "C" }),
                ("Far right     ", new[] { "M", "KD", "SD" }),
                ("Gamla vänstern", new[] { "S", "V", "MP" })
            }
        .Select(x => (x.Block, PartyCodes: x.Abbreviations.Select(metaData.GetPartyCode).ToList()));

        foreach (var item in blocks)
        {
            var votes = votesData.PartyVotes.Where(x => item.PartyCodes.Contains(x.Key)).Sum(x => x.Value);
            var mandat = seats.Where(x => item.PartyCodes.Contains(x.Key)).Sum(x => x.Value);
            Console.WriteLine($"{item.Block}: {votes / (double)votesData.TotalVotes:#.00%} ({mandat})");
        };
    }
    static async Task<(Dictionary<string, RostData>, Dictionary<string, MandatData>)> FetchAndProcess(Uri baseUri, string basePath, string rakningstillfalle)
    {
        var voteMap = new Dictionary<string, RostData>();
        var seatMap = new Dictionary<string, MandatData>();

        Directory.CreateDirectory(basePath);

        List<IndexRecord>? files = null;
        try
        {
            files = (await IndexFile.GetIndexAsync(baseUri)).ToList();
            IndexFile.SaveIndexFile(files, basePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            // Reconstruct latest index data from files if index file is empty - only used when playing around before the election
            Console.WriteLine("No files on site");
            if (files == null || files?.Count < 3)
            {
                files = (await IndexFile.ReconstructIndexAsync(basePath)).ToList();
                IndexFile.SaveReconstructedFiles(files, Path.Combine(basePath, "reconstructed"));
            }
        }
        if (files == null)
            throw new InvalidDataException("No data");

        foreach (var line in files)
        {
            var path = Path.Combine(basePath, line.Hash);
            if (!File.Exists(path))
            {
                Console.WriteLine($"Downloading {line.Path}");
                await Utils.SaveFileAsync(baseUri, line.Path, path);
            }

            var districtData = await Utils.ReadZipFileAsync(path);
            if (districtData.Seats.rakningstillfalle == rakningstillfalle)
            {
                voteMap.Add(districtData.District.kod, districtData.Votes);
                seatMap.Add(districtData.District.kod, districtData.Seats);
            }
        }

        return (voteMap, seatMap);
    }

    record VotesData(Dictionary<string, int> PartyVotes, int TotalVotes, int TotalDistricts, int CountedDistricts);
    private static VotesData CalcVotes(IEnumerable<Valdistrikt> districts)
    {
        var votecount = districts.SelectMany(y => y.rostfordelning?.rosterPaverkaMandat.partiRoster ?? Enumerable.Empty<Partiroster>());
        var partyVotes = votecount.GroupBy(x => x.partikod).ToDictionary(x => x.Key, x => x.Sum(y => y.antalRoster));
        var countedDistricts = districts.Count(x => !string.IsNullOrEmpty(x.rapporteringsTid));
        var numDistricts = districts.Count();

        var totalVotes = votecount.Sum(x => x.antalRoster) + districts.Sum(x => x.rostfordelning?.rosterEjPaverkaMandat.antalRoster ?? 0);

        return new VotesData(partyVotes, totalVotes, numDistricts, countedDistricts);
    }

    private static VotesData AdjustForOnsdagsVotes2018(VotesData votesData)
    {
        var onsdagsVotes = new List<(string Partikod, int Votes)> {
                ("0002", 47544), ("0004", 18407), ("0055", 13655), ("0005", 20403),
                ("0001", 43088), ("0003", 11862), ("0068", 10294), ("0110", 30147)
            }
        .ToDictionary(x => x.Partikod, x => x.Votes);

        var modifiedPartyVotes = votesData.PartyVotes
            .ToDictionary(x => x.Key, x => x.Value + (onsdagsVotes.TryGetValue(x.Key, out var adjustment) ? adjustment : 0));
        var modifiedTotalVotes = votesData.TotalVotes + onsdagsVotes.Values.Sum();

        return votesData with { PartyVotes = modifiedPartyVotes, TotalVotes = modifiedTotalVotes };
    }

    private static void PrintHeader(IEnumerable<PartyData> parties)
    {
        Console.Write($"{" ",20}");
        foreach (var party in parties)
        {
            Console.Write($"| {party.Abbreviation,6} ");
        }
        Console.WriteLine("|");
    }

    private static void PrintCount(VotesData votesData, IEnumerable<PartyData> parties, string header)
    {
        Console.Write($"{header,20}");
        foreach (var party in parties)
        {
            Console.Write($"|{(votesData.PartyVotes.TryGetValue(party.Code, out var voteCount) ? voteCount / (double)votesData.TotalVotes : 0),7:#.0%} ");
        }
        Console.WriteLine($"| {votesData.CountedDistricts,4}/{votesData.TotalDistricts,4}");
    }

}