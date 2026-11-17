using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ThoraxLab.Learning;

static class MuseumCollectionTests
{
    public static void Run(string anatomyPath, Action<bool, string> check)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(anatomyPath));
        string Read(JsonElement record, string key) => record.TryGetProperty(key, out var value) ? value.GetString() : null;
        var entries = json.RootElement.GetProperty("records").EnumerateArray().Select(r => new MuseumEntry(
            Read(r, "id"), Read(r, "name"), Read(r, "system"), Read(r, "required_group"), Read(r, "tissue"),
            r.TryGetProperty("reference_region", out var reference) && reference.GetBoolean())).ToArray();
        var museum = new MuseumCollection(entries);
        check(museum.Entries.Count == 249 && museum.Systems.Length == 8, "The museum must cover all 249 catalog entries across all eight systems.");
        check(museum.Entries.Count(e => e.IsReference) == 6, "The museum includes all six reference annotations as labeled exhibits.");
        var visited = museum.Systems.SelectMany(s => Enumerable.Range(0, museum.PageCount(s)).SelectMany(p => museum.Page(s, p))).ToArray();
        check(visited.Length == entries.Length && visited.Select(e => e.Id).Distinct().Count() == entries.Length &&
            entries.All(e => visited.Contains(e)), "Paging must make every catalog entry reachable exactly once.");
        foreach (var system in museum.Systems)
        {
            check(museum.Page(system, -1).SequenceEqual(museum.Page(system, 0)), system + ": negative page index must stay on the first display.");
            check(museum.Page(system, int.MaxValue).SequenceEqual(museum.Page(system, museum.PageCount(system) - 1)), system + ": large page index must stay on the final display.");
            for (int page = 0; page < museum.PageCount(system); page++)
                check(museum.Page(system, page).Length > 0 && museum.Page(system, page).Length <= 6 && museum.Page(system, page).All(e => e.System == system),
                    system + ": each display contains at most six exhibits from the selected system.");
        }
        foreach (var entry in museum.Entries)
        {
            check(!string.IsNullOrWhiteSpace(entry.Name) && entry.Description.Length >= 50 && entry.Description.Length <= 200,
                entry.Id + ": every exhibit needs a name and a brief, readable description.");
            check(!entry.Description.Contains("displayed separately so its shape"), entry.Id + ": existing anatomy needs a specific description, not the future-record fallback.");
            check(ReferenceEquals(museum.Find(entry.Id), entry) && museum.Search(entry.Name).Contains(entry), entry.Id + ": every named exhibit can be found and focused.");
        }
        check(museum.Search("  VaLvE  ").Length > 0 && museum.Search("  VaLvE  ").Select(e => e.Id).SequenceEqual(museum.Search("valve").Select(e => e.Id)), "Search must ignore case and surrounding whitespace.");
        check(museum.Search("No such anatomy 987654321").Length == 0 && museum.Page("No such system", 0).Length == 0 && museum.Find("missing") == null,
            "Empty searches and unknown identities must not select an unrelated exhibit.");
        check(museum.Entries.Where(e => e.IsReference).All(e => e.Description.Contains("Reference") && (e.Description.Contains("not") || e.Description.Contains("rather than"))),
            "Cavities and septal region markers must not be described as actual separate tissue walls.");
        check(museum.Find("REC_Right_Visceral_Pleura").Description.Contains("lung surface") && museum.Find("REC_Right_Parietal_Pleura").Description.Contains("chest wall"),
            "Visceral and parietal pleura descriptions must distinguish the two surfaces.");
        check(museum.Entries.Where(e => e.Group == "pulmonary_vessels").Select(e => e.Description).Distinct().Count() == 2,
            "Pulmonary artery and vein descriptions must distinguish blood direction and oxygenation.");
        check((int)LearningMode.AnatomyCases == 0 && (int)LearningMode.GuidedDissection == 3 && Enum.GetValues<LearningMode>().Length == 4,
            "An ungraded museum must not change serialized assessment mode identities or server rubrics.");
        bool duplicateRejected = false;
        try { _ = new MuseumCollection(new[] { entries[0], entries[0] }); } catch (ArgumentException) { duplicateRejected = true; }
        check(duplicateRejected, "Duplicate catalog identities must not silently hide an exhibit.");
    }
}
