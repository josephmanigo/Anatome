using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class ThoraxMuseum : MonoBehaviour
    {
        LabRuntime app;
        readonly Dictionary<string, AnatomyStructure> originals = new Dictionary<string, AnatomyStructure>();
        readonly List<MuseumExhibit> exhibits = new List<MuseumExhibit>();
        Dictionary<string, Mesh> lungDetail;
        GameObject room, displays;
        GameObject galleryPrefab, standPrefab;
        TextMeshPro gallerySign;
        Coroutine entering;
        Transform rig;
        Vector3 savedRigPosition, savedPanelPosition;
        Quaternion savedRigRotation, savedPanelRotation;
        int blockedUntil;
        float labelScale;
        bool labelContrast;
        public MuseumCollection Collection { get; private set; }
        public bool IsActive { get; private set; }
        public bool ChangingGallery => IsActive && Time.frameCount <= blockedUntil;
        public string SystemName { get; private set; } = "Skeletal";
        public int PageIndex { get; private set; }
        public MuseumExhibit Selected { get; private set; }
        public IReadOnlyList<MuseumExhibit> Exhibits => exhibits;
        public IEnumerable<Renderer> InspectionRenderers => Selected ? new Renderer[] { Selected.Renderer } : System.Array.Empty<Renderer>();

        public void Initialize(LabRuntime runtime)
        {
            app = runtime;
            foreach (var structure in app.Session.originals.Where(s => s && !s.fragment)) originals.Add(structure.record.id, structure);
            Collection = new MuseumCollection(originals.Values.Select(s => new MuseumEntry(s.record.id, s.record.name,
                s.record.system, s.record.required_group, s.record.tissue, s.record.reference_region)));
        }

        public void Open()
        {
            if (IsActive || Collection.Entries.Count == 0) return;
            if (!galleryPrefab) galleryPrefab = Resources.Load<GameObject>("Learning/Museum/Gallery");
            if (!standPrefab) standPrefab = Resources.Load<GameObject>("Learning/Museum/Plinth");
            if (!galleryPrefab || !standPrefab) { Debug.LogError("Museum gallery or plinth prefab is missing."); return; }
            rig = Camera.main.transform.parent;
            savedRigPosition = rig.position; savedRigRotation = rig.rotation;
            savedPanelPosition = app.UI.Root.position; savedPanelRotation = app.UI.Root.rotation;
            var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            room = Instantiate(galleryPrefab, new Vector3(20, 0, 0), Quaternion.LookRotation(forward));
            room.name = "Thorax Museum galleries";
            gallerySign = new GameObject("Gallery directory").AddComponent<TextMeshPro>();
            gallerySign.transform.SetParent(room.transform, false);
            gallerySign.transform.localPosition = new Vector3(0, 2.05f, 6.21f);
            gallerySign.rectTransform.sizeDelta = new Vector2(4.35f, 2.15f);
            if (LabWidgets.Font) gallerySign.font = LabWidgets.Font;
            gallerySign.fontSize = 4.25f;
            gallerySign.alignment = TextAlignmentOptions.Center;
            gallerySign.textWrappingMode = TextWrappingModes.NoWrap;
            gallerySign.raycastTarget = false;
            gallerySign.color = new Color(.99f, .96f, .87f);
            IsActive = true;
            blockedUntil = Time.frameCount + 3;
            entering = StartCoroutine(EnterAfterRelease());
        }

        IEnumerator EnterAfterRelease()
        {
            // Finish XRI release in the lab before moving the rig to the gallery.
            // A held source model must never follow the controller into the museum.
            yield return null;
            yield return null;
            entering = null;
            if (!app.Focused || (!app.XRReady && !LabRuntime.VerificationRunning))
            { Close(); app.UI.ShowHub(); yield break; }
            app.Session.SetMode(LabMode.Explore); app.Presentation.ShowIntact();
            MoveRig(rig.position + room.transform.position - Vector3.ProjectOnPlane(Camera.main.transform.position, Vector3.up), rig.rotation);
            ShowSystem(SystemName);
            if (app.UI.WorkspaceVisible) app.UI.ShowWorkspace();
            else app.UI.RecenterPanel();
        }

        void MoveRig(Vector3 position, Quaternion rotation)
        {
            if (!rig) return;
            var controller = rig.GetComponent<CharacterController>(); bool enabled = controller && controller.enabled;
            if (enabled) controller.enabled = false;
            rig.SetPositionAndRotation(position, rotation);
            if (enabled) controller.enabled = true;
            Physics.SyncTransforms();
        }

        public void ShowSystem(string system)
        {
            if (!IsActive || entering != null || !Collection.Systems.Contains(system)) return;
            SystemName = system; PageIndex = 0; BuildDisplays();
        }
        public void TurnPage(int direction)
        {
            if (!IsActive || entering != null) return;
            PageIndex = Mathf.Clamp(PageIndex + direction, 0, Mathf.Max(0, Collection.PageCount(SystemName) - 1));
            BuildDisplays();
        }
        public void FocusEntry(string id)
        {
            var entry = Collection.Find(id); if (!IsActive || entering != null || entry == null) return;
            SystemName = entry.System;
            PageIndex = System.Array.FindIndex(Collection.InSystem(SystemName), e => e.Id == id) / MuseumCollection.PageSize;
            BuildDisplays(); Select(exhibits.First(e => e.Entry.Id == id));
        }

        void BuildDisplays()
        {
            if (app.Inspector) app.Inspector.Close();
            blockedUntil = Time.frameCount + 2; // Release held parts before another display can be grabbed.
            Selected = null; exhibits.Clear();
            if (displays) { displays.SetActive(false); Destroy(displays); }
            displays = new GameObject("Labeled museum exhibits"); displays.transform.SetParent(room.transform, false);
            float top = Mathf.Clamp(Camera.main.transform.position.y - .65f, .7f, 1.2f);
            var entries = Collection.Page(SystemName, PageIndex);
            Vector3 localHead = displays.transform.InverseTransformPoint(Camera.main.transform.position);
            gallerySign.text = $"<size=39%><cspace=0.18em>THORAX MUSEUM</cspace></size>\n{SystemName}\n<size=40%>Display {PageIndex + 1} of {Collection.PageCount(SystemName)}</size>";
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; var source = originals[entry.Id];
                var position = new Vector3((i % 3 - 1) * 1.4f, top, 2 + (i / 3) * 2.1f);
                var stand = Instantiate(standPrefab, displays.transform);
                stand.name = "Stand · " + entry.Name;
                stand.transform.localPosition = new Vector3(position.x, 0, position.z);
                stand.transform.localScale = new Vector3(1, top, 1);
                var model = new GameObject("Exhibit · " + entry.Id);
                model.transform.SetParent(displays.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = source.Filter.sharedMesh;
                var renderer = model.AddComponent<MeshRenderer>(); renderer.sharedMaterials = source.Renderer.sharedMaterials;
                var bounds = source.Filter.sharedMesh.bounds;
                // The imported catalog includes valid sub-millimetre mesh bounds before its source transform.
                float radius = bounds.extents.magnitude;
                float scale = radius > 0 ? .24f / radius : 1f;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localRotation = Quaternion.Inverse(room.transform.rotation) * source.transform.rotation;
                model.transform.position = room.transform.TransformPoint(position + Vector3.up * .32f) - model.transform.TransformVector(bounds.center);
                var collider = model.AddComponent<MeshCollider>(); collider.sharedMesh = source.Filter.sharedMesh;
                var exhibit = model.AddComponent<MuseumExhibit>(); exhibit.Initialize(this, entry, position);
                exhibits.Add(exhibit);
                exhibit.BuildLabel(displays.transform);
                FaceLabel(exhibit, localHead);
            }
            labelScale = app.Settings.Current.EffectiveTextScale; labelContrast = app.Settings.Current.highContrast;
            if (exhibits.Count > 0) Select(exhibits[0]);
            Physics.SyncTransforms();
        }

        static void FaceLabel(MuseumExhibit exhibit, Vector3 localHead)
        {
            Vector3 outward = Vector3.ProjectOnPlane(localHead - exhibit.StandTop, Vector3.up).normalized;
            if (outward.sqrMagnitude < .01f) outward = Vector3.back;
            // The support distance keeps the entire plaque outside the beveled stand, even from a corner.
            float clearance = Mathf.Abs(outward.x) * .415f + Mathf.Abs(outward.z) * .335f + .016f;
            var position = exhibit.StandTop + outward * clearance;
            float labelHeight = exhibit.Label.rect.height * exhibit.Label.localScale.y;
            position.y = Mathf.Max(position.y - .025f, labelHeight + .03f);
            exhibit.Label.localPosition = position;
            exhibit.Label.localRotation = Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(12, 0, 0);
        }

        public void Select(MuseumExhibit exhibit)
        {
            if (!IsActive || !exhibits.Contains(exhibit)) return;
            if (Selected && Selected != exhibit)
            {
                Selected.ApplyLabelStyle(false);
                if (Selected.Entry.Group == "lung_lobes")
                    Selected.GetComponent<MeshFilter>().sharedMesh = originals[Selected.Entry.Id].Filter.sharedMesh;
            }
            if (exhibit.Entry.Group == "lung_lobes")
            {
                lungDetail ??= Resources.LoadAll<Mesh>("Learning/AtlasLungDetail").ToDictionary(m => m.name);
                if (lungDetail.TryGetValue(exhibit.Entry.Id, out var mesh))
                    exhibit.GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            if (Selected != exhibit) exhibit.ApplyLabelStyle(true);
            Selected = exhibit;
            if (app.UI) app.UI.RefreshMuseumSelection();
        }
        public void ResetExhibits()
        {
            if (!IsActive || entering != null) return;
            BuildDisplays();
        }
        void LateUpdate()
        {
            if (!IsActive || !displays || !Camera.main) return;
            bool refresh = labelScale != app.Settings.Current.EffectiveTextScale || labelContrast != app.Settings.Current.highContrast;
            Vector3 localHead = displays.transform.InverseTransformPoint(Camera.main.transform.position);
            foreach (var exhibit in exhibits)
            {
                if (!exhibit) continue;
                if (refresh) exhibit.ApplyLabelStyle(exhibit == Selected);
                FaceLabel(exhibit, localHead);
            }
            labelScale = app.Settings.Current.EffectiveTextScale; labelContrast = app.Settings.Current.highContrast;
        }
        public void Close()
        {
            if (!IsActive && !room) return;
            if (entering != null) StopCoroutine(entering);
            entering = null;
            if (app && app.Inspector) app.Inspector.Close();
            IsActive = false; Selected = null; exhibits.Clear();
            if (room) { room.SetActive(false); Destroy(room); } room = displays = null;
            gallerySign = null;
            MoveRig(savedRigPosition, savedRigRotation);
            if (app && app.UI) app.UI.Root.SetPositionAndRotation(savedPanelPosition, savedPanelRotation);
        }
        void OnDestroy() => Close();
        void OnDisable() => Close();
    }
}
