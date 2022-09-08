using Qaplix.Val;
using System.IO;
using System.Security.Cryptography.X509Certificates;

var baseUri = new Uri("https://resultat.val.se/resultatfiler/");
var basePath = @"torsdag/slutlig/";
var rakningstillfalle = "slutlig";

var voteMap = new Dictionary<string, RostData>();
var seatMap = new Dictionary<string, MandatData>();

Directory.CreateDirectory(basePath);

List<IndexRecord>? files = null;
try
{
    files = (await IndexFile.GetIndexAsync(baseUri)).ToList();
    IndexFile.SaveIndexFile(files, basePath);
}
catch(Exception ex)
{
    Console.WriteLine(ex.Message);
    // Reconstruct latest index data from files if index file is empty - only used when playing around before the election
    Console.WriteLine("No files on site");
    if (files==null || files?.Count < 3)
    {
        files = (await IndexFile.ReconstructIndexAsync(basePath)).ToList();
        IndexFile.SaveReconstructedFiles(files, Path.Combine(basePath, "reconstructed"));
    }
}

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

var state = seatMap.Values.Where(x => x.valtyp == "RD");
try
{
    Console.WriteLine("\nMandatfördelning");
    foreach (var party in state
        .SelectMany(x => x.valomrade.mandatfordelning?.partiLista ?? Enumerable.Empty<Partilista>())
        .GroupBy(x => x.partiforkortning))
    {
        Console.WriteLine($"{party.Key} {party.Sum(x => x.antalMandat)}");
    }

    Console.WriteLine("\nRäknade distrikt");
    foreach (var region in seatMap.Values.Where(x => x.valtyp == "RD"))
    {
        Console.WriteLine($"{region.valtyp} {region.valomrade.kod} {region.valomrade.namn} {region.valomrade.antalValdistriktRaknade}/{region.valomrade.antalValdistriktSomSkaRaknas}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nCould not list seat data: {ex.Message}");
}

var stateVotes = voteMap.Values.Where(x => x.valtyp == "RD");
try
{
    Console.WriteLine("\nRäknade distrikt");
    var partyVotes = stateVotes
        .SelectMany(x => x.valdistrikt)
        .SelectMany(x => x.rostfordelning?.rosterPaverkaMandat.partiRoster ?? Enumerable.Empty<Partiroster>());
    var maxLen = partyVotes.Max(x => x.partibeteckning.Length);
    foreach (var party in partyVotes
        .GroupBy(x => x.partikod)
        .Select(x => (Info: x.First(), Votes: x.Sum(x => x.antalRoster))))
    {
        Console.WriteLine($"{party.Info.partiforkortning,4} {party.Votes,8} {party.Info.partibeteckning}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Could not list votes data: {ex.Message}");
}
