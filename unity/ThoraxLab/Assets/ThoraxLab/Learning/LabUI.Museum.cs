using System;
using System.Linq;
using TMPro;
using UnityEngine;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        TextMeshProUGUI museumSelection;

        void MuseumWorkspace()
        {
            var museum = app.Museum;
            Text(content, museum.SystemName + " · " + museum.Collection.InSystem(museum.SystemName).Length + " exhibits", 27, Accent);
            var pages = Row(content, "Museum display pages", 10);
            Button(pages, "Previous", () => { museum.TurnPage(-1); ShowWorkspace(); }, false, museum.PageIndex > 0);
            Button(pages, "Next", () => { museum.TurnPage(1); ShowWorkspace(); }, false, museum.PageIndex + 1 < museum.Collection.PageCount(museum.SystemName));
            Text(content, "Display " + (museum.PageIndex + 1) + " of " + museum.Collection.PageCount(museum.SystemName) +
                " · " + museum.Collection.Entries.Count + " exhibits across all galleries", 22, Muted);
            Button(content, "Choose a gallery / find a part", ShowMuseumCollection, true);
            museumSelection = Text(content, "", 25, Accent); RefreshMuseumSelection();
            Button(content, "Inspect / zoom selected exhibit", app.Inspector.Open);
            Button(content, "Return exhibits to their stands", museum.ResetExhibits);
            Text(content, "Every exhibit has its name and a brief description. Point and press trigger to select; grip to lift and turn. Release to return it to its stand. Models are resized for viewing.", 23, Muted);
            Text(content, InspectionInstructions + " " + MovementSummary(), 22, Muted);
            Text(content, "Explore freely. Museum visits have no score and do not change assessment progress.", 22, Muted);
            Text(content, "After walking through the gallery, press Menu to bring the controls back beside you.", 22, Muted);
            Button(content, "Leave museum", () => Leave(ShowHub));
        }

        public void RefreshMuseumSelection()
        {
            if (museumSelection && app.Museum && app.Museum.Selected)
                museumSelection.text = "Selected: " + app.Museum.Selected.Entry.Name + "\n" + app.Museum.Selected.Entry.Description;
        }

        void ShowMuseumCollection()
        {
            Page("Museum collection", "Find an anatomy exhibit");
            Button(content, "Return to gallery", ShowWorkspace, true);
            var search = Field("Search structure name or description");
            var results = Column(content, "Museum search and galleries");
            int page = 0;
            Action render = null;
            render = () =>
            {
                foreach (Transform child in results) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                var collection = app.Museum.Collection;
                if (string.IsNullOrWhiteSpace(search.text))
                {
                    Text(results, "Choose a body system", 26, Accent);
                    foreach (string system in collection.Systems)
                    {
                        string selected = system;
                        Button(results, system + " · " + collection.InSystem(system).Length + " exhibits",
                            () => { app.Museum.ShowSystem(selected); ShowWorkspace(); });
                    }
                    return;
                }
                var matches = collection.Search(search.text);
                int count = Math.Max(1, (matches.Length + 7) / 8); page = Math.Min(page, count - 1);
                Text(results, matches.Length + " matching exhibits", 24, Accent);
                foreach (var entry in matches.Skip(page * 8).Take(8))
                {
                    string id = entry.Id;
                    Button(results, entry.Name + " · " + entry.System, () => { app.Museum.FocusEntry(id); ShowWorkspace(); });
                }
                if (matches.Length == 0) Text(results, "Try another structure name, such as rib, bronchus or valve.", 23, Muted);
                if (count > 1)
                {
                    var navigation = Row(results, "Search pages", 10);
                    Button(navigation, "Previous results", () => { page--; render(); }, false, page > 0);
                    Button(navigation, "Next results", () => { page++; render(); }, false, page + 1 < count);
                    Text(results, "Results page " + (page + 1) + " of " + count, 22, Muted);
                }
            };
            search.onValueChanged.AddListener(_ => { page = 0; render(); }); render();
        }
    }
}
