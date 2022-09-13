using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Qaplix.Val;

internal record IndexRecord(string Hash, string Path);
internal record DistrictData(Valomrade District, RostData Votes, MandatData Seats);

internal class IndexFile
{
    private EntityTagHeaderValue? _etag = null;
    private Uri _baseUri { get; }
    private string _basePath { get; }

    private List<IndexRecord>? _indexCache;
    private Dictionary<string, DistrictData> _dataCache = new Dictionary<string, DistrictData>();

    public IEnumerable<IndexRecord> Data => _indexCache?.AsEnumerable() ?? throw new InvalidDataException("No data available");
    public bool HasData => _indexCache != null;

    public IndexFile(Uri baseUri, string basePath)
    {
        _baseUri = baseUri;
        _basePath = basePath;
    }

    public async Task<bool> RefreshAsync()
    {
        if (await RefreshIndexAsync())
        {
            var updated = await RefreshFilesAsync();
            return updated.Any();
        }
        return false;
    }

    private async Task<IEnumerable<IndexRecord>> RefreshFilesAsync()
    {
        Directory.CreateDirectory(_basePath);

        if (!HasData) throw new InvalidDataException("No data");

        var updatedRecords = new List<IndexRecord>();
        foreach (var record in Data)
        {
            var path = Path.Combine(_basePath, record.Hash);
            if (!File.Exists(path))
            {
                Console.WriteLine($"Downloading {record.Path}");
                await SaveFileAsync(_baseUri, record.Path, path);
                updatedRecords.Add(record);
            }
        }
        return updatedRecords;
    }

    public async IAsyncEnumerable<DistrictData> GetDistrictsAsync()
    {
        foreach (var record in Data)
        {
            if (!_dataCache.TryGetValue(record.Hash, out var districtData))
            {
                districtData = await ReadZipFileAsync(Path.Combine(_basePath, record.Hash));
                _dataCache.Add(record.Hash, districtData);
            }
            yield return districtData;
        }
    }

    private static async Task SaveFileAsync(Uri baseUri, string relativeUrl, string file)
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

    private async Task<bool> RefreshIndexAsync()
    {
        var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, "index.md5"));
        if (_etag!=null)
        {
            request.Headers.IfNoneMatch.Add(_etag);
        }
        var response = await httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotModified)
            return false; // No changes

        // Throw on error
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        _etag = response.Headers.ETag;
        _indexCache = content
            .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2) throw new InvalidDataException($"Entry does not have two parts: {line}");
                if (parts[1] == "-") throw new InvalidDataException("Missing file name");
                return new IndexRecord(parts[0], parts[1]);
            })
            .ToList();

        if (_etag != null)
            Console.WriteLine($"Saving index file for etag ${_etag}");
        SaveIndexFile(_indexCache, _basePath);

        // Rewrite the cache to throw away all updated items
        var newCache = new Dictionary<string, DistrictData>();
        foreach (var item in _indexCache)
        {
            if (_dataCache.TryGetValue(item.Hash, out var districtData)) 
            {
                newCache.Add(item.Hash, districtData);
            }
        }
        _dataCache = newCache;
        return true;
    }

    public static async Task<IEnumerable<IndexRecord>> ReconstructIndexAsync(string path)
    {
        var dictionary = new Dictionary<(string Type, string Code, string Occasion), (DateTime Time, string Hash)>();
        foreach (var file in Directory.EnumerateFiles(path).Where(x => x.Length == 34))
        {
            (var id, var votes, var seats) = await ReadZipFileAsync(file);
            var updateTime = votes.senasteUppdateringstid < seats.senasteUppdateringstid ? seats.senasteUppdateringstid : votes.senasteUppdateringstid;
            if (dictionary.TryGetValue((seats.valtyp, seats.valomrade.kod, seats.rakningstillfalle), out var pair))
            {
                if (pair.Time < seats.senasteUppdateringstid)
                {
                    dictionary[(seats.valtyp, seats.valomrade.kod, seats.rakningstillfalle)] = (seats.senasteUppdateringstid, file);
                }
            }
            else
            {
                dictionary[(seats.valtyp, seats.valomrade.kod, seats.rakningstillfalle)] = (seats.senasteUppdateringstid, file);
            }
        }
        return dictionary.Select(x => new IndexRecord(x.Value.Hash, $@"./{x.Key.Occasion.Substring(0,1)}/{x.Key.Type}/Val_20220911_{RemoveDiacritics(x.Key.Occasion)}_{x.Key.Code}_{x.Key.Type}.zip"));
    }

    // https://blog.fredrikhaglund.se/blog/2008/04/16/how-to-remove-diacritic-marks-from-strings/
    public static string RemoveDiacritics(string s)
    {
        var normalizedString = s.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        for (int i = 0; i < normalizedString.Length; i++)
        {
            char c = normalizedString[i];
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stringBuilder.Append(c);
        }
        return stringBuilder.ToString();
    }

    public static void SaveReconstructedFiles(IEnumerable<IndexRecord> files, string path)
    {
        Directory.CreateDirectory(path);
        foreach (var file in files)
        {
            var fileName = Path.Combine(path, file.Path);
            var directory = Path.GetDirectoryName(fileName);
            Directory.CreateDirectory(directory);
            File.Copy(file.Hash, Path.Combine(path, file.Path), true);
        }
        SaveIndexFile(files, path);
    }

    public static void SaveIndexFile(IEnumerable<IndexRecord> files, string path)
    {
        File.WriteAllLines(Path.Combine(path, "index.md5"), files.Select(file => $"{file.Hash} {file.Path}"));
    }

    private async static Task<DistrictData> ReadZipFileAsync(string file)
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