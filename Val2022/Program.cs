using CommandLine;
using System.Collections.Generic;
using System.Data;

namespace Qaplix.Val;

public static class Program
{
    public class Options
    {
        [Option('w', "wednesday", Default = false, Required = false, HelpText = "Use wednesday votes from 2018 for uppsamlingsdistrikt.")]
        public bool UseWednesdayVotes { get; set; }
        [Value(0, Default = "slutlig", Required = false, HelpText = "Use a specific räkningstillfälle, e.g. preliminär/slutlig.")]
        public string? VoteCountOccasion { get; set; }
        [Option('f', "follow", Default = false, Required = false, HelpText = "Follow changes over time by re-running the analysis.")]
        public bool DoFollow { get; set; }
        [Option('d', "delay", Default = 30, Required = false, HelpText = "Delay between re-runs in seconds.")]
        public int Delay { get; set; }
        [Option('u', "url", Default = "https://resultat.val.se/resultatfiler/", Required = false, HelpText = "Base URL for result files.")]
        public string? Uri { get; set; }
        [Option('p', "path", Default = "results", Required = false, HelpText = "Path to result files.")]
        public string? BasePath { get; set; }
        [Option('e', "election", Default = "RD", Required = false, HelpText = "Election type, e.g. RD/RF/KF.")]
        public string? Election { get; set; }
        [Option('l', "level", Default = 1, Required = false, HelpText = "Grouping level 0-3")]
        public int Level { get; set; }
        [Option('s', "uppsamlingsdistrikt", Default = -1, Required = false, HelpText = "Level for uppsamlingsdistrikt")]
        public int LevelUppsamling { get; set; }
    }

    public static async Task Main(string[] args)
    {
        var options = await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(Run);
    }

    public static async Task Run(Options options)
    {
        var metaDataLoader = new ElectionMetaDataLoader();

        var indexFile = new IndexFile(new Uri(options.Uri!), options.BasePath!);
        bool first = true;
        do
        {
            if (await indexFile.RefreshAsync() || first)
            {
                first = false;
                var metaData = metaDataLoader.Elections[options.Election!];

                Console.WriteLine($"Updating @ {DateTime.Now.ToLongTimeString()}");
                (var voteMap, var seatMap) = await FetchAndProcess(indexFile, options.VoteCountOccasion!);

                var votes = voteMap.Values
                    .Where(x => x.valtyp == options.Election)
                    .SelectMany(x => x.valdistrikt);

                //var election18 = new Election2018();
                //foreach (var district in votes.Where(x => x.valdistriktstyp=="uppsamlingsdistrikt"))
                //{
                //    var votes18 = election18.Map18to22[district.valdistriktskod].Sum(x => election18.Votes[x].Sum(x => x.Value));
                //    var votes22 = district.rostfordelning.rosterPaverkaMandat.antalRoster;
                //    Console.WriteLine($"{district.namn} {votes22} ({votes18})");
                //}

                AnalyzeAndPrint(metaData, options.Level, votes, options.UseWednesdayVotes, options.LevelUppsamling);
                Console.WriteLine($"Updated @ {DateTime.Now.ToLongTimeString()}");
            }
            if (options.DoFollow)
            {
                await Task.Delay(options.Delay * 1000);
                Console.Write('.');
            }
        } while (options.DoFollow);
    }

    private static void AnalyzeAndPrint(ElectionMetaData metaData, int level, IEnumerable<Valdistrikt> allDistricts, bool useWednesdayVotes, int levelUppsamling)
    {
        try
        {
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

            if (level >= 0)
            {
                Console.WriteLine("\nVotes\n");
                AnalyzeAndPrintVotes(metaData, level, allDistricts, popularParties);
            }

            if (levelUppsamling >= 0)
            {
                Console.WriteLine("\nUppsamling\n");
                AnalyzeAndPrintVotes(metaData, levelUppsamling, allDistricts.Where(x => x.valdistriktstyp == "uppsamlingsdistrikt"), popularParties);
            }

            Console.WriteLine("\nSeats\n");
            AnalyzeAndPrintMandates(metaData, votesData, popularParties);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not list votesData data: {ex.Message}");
        }

    }

    private static void AnalyzeAndPrintVotes(ElectionMetaData metaData, int level, IEnumerable<Valdistrikt> districts, List<PartyData> parties)
    {
        PrintHeader(parties);

        var granularity = metaData.Levels[level];
        foreach (var region in districts.GroupBy(granularity.Grouping))
        {
            PrintCount(CalcVotes(region), parties, granularity.Names[region.Key]);
        }

        PrintCount(CalcVotes(districts), parties, "TOTALT", false);
        PrintCount(CalcVotes(districts), parties, "OF VALID");
    }

    private static void AnalyzeAndPrintMandates(ElectionMetaData metaData, VotesData votesData, IEnumerable<PartyData> parties)
    {
        double DelningsTal(int numSeats) => numSeats == 0 ? 1.2 : numSeats * 2 + 1;

        var eligibleParties = votesData.PartyVotes.Where(x => x.Value / (double)votesData.ValidVotes > 0.04);
        var seats = eligibleParties.ToDictionary(x => x.Key, x => 0);
        if (seats.Count == 0)
        {
            Console.WriteLine("No eligible parties, i.e. no votes yet counted.");
            return;
        }
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
            Console.WriteLine($"{item.Block}: {votes / (double)votesData.ValidVotes:#.00%} ({mandat})");
        };
    }

    static async Task<(Dictionary<string, RostData>, Dictionary<string, MandatData>)> FetchAndProcess(IndexFile baseUri, string rakningstillfalle)
    {
        var voteMap = new Dictionary<string, RostData>();
        var seatMap = new Dictionary<string, MandatData>();

        Console.Write("Processing");
        var i = 0;
        await foreach (var districtData in baseUri.GetDistrictsAsync())
        { 
            if (districtData.Seats.rakningstillfalle == rakningstillfalle)
            {
                voteMap.Add(districtData.District.kod, districtData.Votes);
                seatMap.Add(districtData.District.kod, districtData.Seats);
            }
            if (++i%10==0)
                Console.Write(".");
        }
        Console.WriteLine("Done");

        return (voteMap, seatMap);
    }

    private static VotesData CalcVotes(IEnumerable<Valdistrikt> districts)
    {
        var votecount = districts.SelectMany(y => y.rostfordelning?.rosterPaverkaMandat.partiRoster ?? Enumerable.Empty<Partiroster>());
        var partyVotes = votecount.GroupBy(x => x.partikod).ToDictionary(x => x.Key, x => x.Sum(y => y.antalRoster));
        var countedDistricts = districts.Count(x => !string.IsNullOrEmpty(x.rapporteringsTid));
        var numDistricts = districts.Count();

        var validVotes = votecount.Sum(x => x.antalRoster);
        var invalidVotes = districts.Sum(x => x.rostfordelning?.rosterEjPaverkaMandat.antalRoster ?? 0);
        var eligibleVoters = districts.Where(x => x.rostfordelning!=null).Sum(x => x.antalRostberattigade ?? 0);

        return new VotesData(partyVotes, validVotes, invalidVotes, eligibleVoters, numDistricts, countedDistricts);
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
        var modifiedTotalVotes = votesData.ValidVotes + onsdagsVotes.Values.Sum();

        return votesData with { PartyVotes = modifiedPartyVotes, ValidVotes = modifiedTotalVotes };
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

    private static void PrintCount(VotesData votesData, IEnumerable<PartyData> parties, string header, bool onlyValidVotes = true)
    {
        var totalVotes = votesData.ValidVotes + votesData.InvalidVotes;
        var percentBase = onlyValidVotes ? votesData.ValidVotes : totalVotes ;
        Console.Write($"{header,20}");
        foreach (var party in parties)
        {
            Console.Write($"|{(votesData.PartyVotes.TryGetValue(party.Code, out var voteCount) ? voteCount / (double)percentBase : 0),7:#.0%} ");
        }
        Console.Write($"| {votesData.CountedDistricts,4}/{votesData.TotalDistricts,4} ");
        Console.Write($"| {(double)totalVotes/votesData.EligibleVoters:#.0%} ");
        Console.WriteLine("|");
    }

}