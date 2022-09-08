using System.Globalization;
using System.Text;

namespace Qaplix.Val;

internal record IndexRecord(string Hash, string Path);

internal class IndexFile
{
    public static async Task<IEnumerable<IndexRecord>> GetIndexAsync(Uri baseUri)
    {
        var httpClient = new HttpClient();
        var res = await httpClient.GetAsync(new Uri(baseUri, "index.md5"));
        var content = await res.Content.ReadAsStringAsync();
        int i = 0;

        return content
            .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2) throw new InvalidDataException($"Entry does not have two parts: {line}");
                if (parts[1] == "-") throw new InvalidDataException("Missing file name");
                return new IndexRecord(parts[0], parts[1]);
            });
    }

    public static async Task<IEnumerable<IndexRecord>> ReconstructIndexAsync(string path)
    {
        var dictionary = new Dictionary<(string Type, string Code, string Occasion), (DateTime Time, string Hash)>();
        foreach (var file in Directory.EnumerateFiles(path).Where(x => x.Length == 34))
        {
            (var id, var votes, var seats) = await Utils.ReadZipFileAsync(file);
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
}