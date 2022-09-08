using System.IO.Compression;
using System.Text.Json;
using static Qaplix.Val.Utils;

namespace Qaplix.Val;

internal class Utils
{
    public static async Task SaveFileAsync(Uri baseUri, string relativeUrl, string file)
    {
        var httpClient = new HttpClient();
        var url = new Uri(baseUri, relativeUrl);
        var res = await httpClient.GetAsync(url);
        if (res.IsSuccessStatusCode)
        {
            var httpBuf = await res.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(file, httpBuf);
        }
    }

    public record DistrictData(Valomrade District, RostData Votes, MandatData Seats);
    public async static Task<DistrictData> ReadZipFileAsync(string file)
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

        RostData? voteData = null;
        MandatData? seatData = null;

        foreach (var entry in zip.Entries.Where(x => x.Name.EndsWith("json")))
        {
            var zipStream = entry.Open();
            if (entry.Name.Contains("rostfordelning"))
            {
                if (voteData != null)
                    throw new InvalidDataException("Duplicate vote data in file.");
                voteData = await JsonSerializer.DeserializeAsync<RostData>(zipStream);
            }
            else
            {
                if (seatData != null)
                    throw new InvalidDataException("Duplicate vote data in file.");
                seatData = await JsonSerializer.DeserializeAsync<MandatData>(zipStream);
            }
        }

        if (voteData == null) throw new InvalidDataException("Vote data missing from file.");
        if (seatData == null) throw new InvalidDataException("Seat data missing from file.");

        return new DistrictData(seatData.valomrade, voteData, seatData);
    }
}

