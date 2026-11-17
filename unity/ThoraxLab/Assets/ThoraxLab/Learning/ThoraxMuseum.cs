using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed class ThoraxMuseum : MonoBehaviour
    {
        LabRuntime app;
        readonly Dictionary<string, AnatomyStructure> originals = new Dictionary<string, AnatomyStructure>();
        readonly List<MuseumExhibit> exhibits = new List<MuseumExhibit>();
        readonly List<Material> materials = new List<Material>();
        GameObject room, displays;
        Coroutine entering;
        Transform rig;
        Vector3 savedRigPosition, savedPanelPosition;
        Quaternion savedRigRotation, savedPanelRotation;
        Material plinthMaterial;
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
            rig = Camera.main.transform.parent;
            savedRigPosition = rig.position; savedRigRotation = rig.rotation;
            savedPanelPosition = app.UI.Root.position; savedPanelRotation = app.UI.Root.rotation;
            var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            room = new GameObject("Thorax Museum galleries");
            room.transform.SetPositionAndRotation(new Vector3(20, 0, 0), Quaternion.LookRotation(forward));
            var floorMaterial = SurfaceMaterial("Museum floor", new Color(.055f, .09f, .12f));
            var wallMaterial = SurfaceMaterial("Museum walls", new Color(.12f, .18f, .22f));
            plinthMaterial = SurfaceMaterial("Museum exhibit stands", new Color(.08f, .23f, .25f));
            Solid("Gallery floor", new Vector3(0, -.06f, 2), new Vector3(8, .12f, 9), floorMaterial);
            Solid("Left wall", new Vector3(-4, 2, 2), new Vector3(.12f, 4, 9), wallMaterial);
            Solid("Right wall", new Vector3(4, 2, 2), new Vector3(.12f, 4, 9), wallMaterial);
            Solid("Back wall", new Vector3(0, 2, 6.5f), new Vector3(8, 4, .12f), wallMaterial);
            Solid("Entry wall", new Vector3(0, 2, -2.5f), new Vector3(8, 4, .12f), wallMaterial);
            var light = new GameObject("Museum exhibit light").AddComponent<Light>();
            light.transform.SetParent(room.transform, false); light.transform.localPosition = new Vector3(0, 3.3f, 2);
            light.type = LightType.Point; light.range = 12; light.intensity = 2.2f; light.shadows = LightShadows.None;
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

        Material SurfaceMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", .2f); materials.Add(material); return material;
        }
        GameObject Solid(string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(room.transform, false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material; return part;
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
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; var source = originals[entry.Id];
                var position = new Vector3((i % 3 - 1) * 1.4f, top, 2 + (i / 3) * 2.1f);
                var stand = Solid("Stand · " + entry.Name, position - Vector3.up * top * .5f, new Vector3(.8f, top, .65f), plinthMaterial);
                stand.transform.SetParent(displays.transform, true);
                var model = new GameObject("Exhibit · " + entry.Id);
                model.transform.SetParent(displays.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = source.Filter.sharedMesh;
                var renderer = model.AddComponent<MeshRenderer>(); renderer.sharedMaterials = source.Renderer.sharedMaterials;
                var bounds = source.Filter.sharedMesh.bounds;
                float scale = .24f / Mathf.Max(.001f, bounds.extents.magnitude);
                model.transform.localScale = Vector3.one * scale;
                model.transform.localRotation = Quaternion.Inverse(room.transform.rotation) * source.transform.rotation;
                model.transform.position = room.transform.TransformPoint(position + Vector3.up * .32f) - model.transform.TransformVector(bounds.center);
                var collider = model.AddComponent<MeshCollider>(); collider.sharedMesh = source.Filter.sharedMesh;
                var exhibit = model.AddComponent<MuseumExhibit>(); exhibit.Initialize(this, entry);
                exhibits.Add(exhibit);
                MakeLabel(exhibit, position);
            }
            labelScale = app.Settings.Current.textScale; labelContrast = app.Settings.Current.highContrast;
            if (exhibits.Count > 0) Select(exhibits[0]);
            Physics.SyncTransforms();
        }

        void MakeLabel(MuseumExhibit exhibit, Vector3 standTop)
        {
            if (exhibit.Label) { exhibit.Label.gameObject.SetActive(false); Destroy(exhibit.Label.gameObject); }
            var plate = Rect("Label · " + exhibit.Entry.Name, displays.transform);
            plate.pivot = new Vector2(.5f, 1); plate.sizeDelta = new Vector2(880, 0); plate.localScale = Vector3.one * .0012f;
            plate.localPosition = standTop + new Vector3(0, -.02f, -.39f); plate.localRotation = Quaternion.identity;
            var canvas = plate.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
            var backing = plate.gameObject.AddComponent<Image>(); backing.color = app.Settings.Current.highContrast ? Color.black : new Color(.018f, .035f, .045f); backing.raycastTarget = false;
            var layout = plate.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(26, 26, 20, 20);
            layout.spacing = 10; layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
            var fit = plate.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var name = Text(plate, exhibit.Entry.Name, 34, Color.white); name.fontStyle = FontStyles.Bold; name.raycastTarget = false;
            var description = Text(plate, exhibit.Entry.Description, 26, Color.white); description.raycastTarget = false;
            exhibit.Label = plate;
        }

        public void Select(MuseumExhibit exhibit)
        {
            if (!IsActive || !exhibits.Contains(exhibit)) return;
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
            bool inspecting = app.Inspector && app.Inspector.IsOpen && app.UI.ScreenName == "Model inspection";
            if (displays.activeSelf == inspecting) displays.SetActive(!inspecting);
            bool refresh = labelScale != app.Settings.Current.textScale || labelContrast != app.Settings.Current.highContrast;
            foreach (var exhibit in exhibits)
            {
                if (!exhibit) continue;
                if (refresh)
                {
                    var position = displays.transform.InverseTransformPoint(exhibit.Label.position) - new Vector3(0, -.02f, -.39f);
                    MakeLabel(exhibit, position);
                }
                if (exhibit.Label) exhibit.Label.rotation = Quaternion.LookRotation(exhibit.Label.position - Camera.main.transform.position, Vector3.up);
            }
            labelScale = app.Settings.Current.textScale; labelContrast = app.Settings.Current.highContrast;
        }
        public void Close()
        {
            if (!IsActive && !room) return;
            if (entering != null) StopCoroutine(entering);
            entering = null;
            if (app && app.Inspector) app.Inspector.Close();
            IsActive = false; Selected = null; exhibits.Clear();
            if (room) { room.SetActive(false); Destroy(room); } room = displays = null;
            foreach (var material in materials) if (material) Destroy(material);
            materials.Clear();
            MoveRig(savedRigPosition, savedRigRotation);
            if (app && app.UI) app.UI.Root.SetPositionAndRotation(savedPanelPosition, savedPanelRotation);
        }
        void OnDestroy() => Close();
        void OnDisable() => Close();
    }
}
