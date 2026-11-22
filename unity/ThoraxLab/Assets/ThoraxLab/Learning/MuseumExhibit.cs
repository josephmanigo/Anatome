using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    // Museum copies are grabbable displays, never AnatomyStructure assessment targets.
    public sealed class MuseumExhibit : MonoBehaviour
    {
        internal static readonly Color Enamel = new Color(.075f, .19f, .17f);
        internal static readonly Color Ivory = new Color(.96f, .92f, .82f);
        internal static readonly Color Brass = new Color(.81f, .69f, .48f);
        ThoraxMuseum museum;
        Vector3 homePosition;
        Quaternion homeRotation;
        TextMeshProUGUI title, description;
        LabIcon selectedMark;
        Image inlay;
        public MuseumEntry Entry { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public RectTransform Label { get; private set; }
        public Vector3 StandTop { get; private set; }

        public void Initialize(ThoraxMuseum owner, MuseumEntry entry, Vector3 standTop)
        {
            museum = owner; Entry = entry; StandTop = standTop; Renderer = GetComponent<MeshRenderer>();
            homePosition = transform.localPosition; homeRotation = transform.localRotation;
            var body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.movementType = XRBaseInteractable.MovementType.Instantaneous; Grab.throwOnDetach = false;
            Grab.useDynamicAttach = true; Grab.matchAttachPosition = true; Grab.matchAttachRotation = true;
            Grab.snapToColliderVolume = false; Grab.unparentTransformOnGrab = false;
            Grab.selectEntered.AddListener(_ => Select());
            Grab.selectExited.AddListener(_ => ReturnToStand());
        }

        internal void BuildLabel(Transform parent)
        {
            Label = Column(parent, "Label · " + Entry.Name, 22, 9);
            Label.pivot = new Vector2(.5f, 1);
            Label.localScale = Vector3.one * .00108f;
            var canvas = Label.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var face = Label.gameObject.AddComponent<Image>();
            face.sprite = LabUIVisuals.Rounded;
            face.type = Image.Type.Sliced;
            face.raycastTarget = false;
            Label.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var heading = Row(Label, "Structure heading", 12);
            var row = heading.GetComponent<HorizontalLayoutGroup>();
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            title = Text(heading, Entry.Name, 42);
            title.fontStyle = FontStyles.Bold;
            var headingSize = Size(title, flexible: 1);
            headingSize.minWidth = headingSize.preferredWidth = 0;
            selectedMark = LabUIVisuals.Icon(heading, LabGlyph.Check);
            inlay = Panel(Label, "Brass inlay", Brass);
            Size(inlay, 3);
            inlay.raycastTarget = false;
            description = Text(Label, Entry.Description, 32);
            ApplyLabelStyle(false);
        }

        internal void ApplyLabelStyle(bool selected)
        {
            // Keep even large-text plaques within the width of their plinth.
            Label.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Lerp(670, 760, Mathf.InverseLerp(1, 1.5f, TextScale)));
            Label.GetComponent<Image>().color = HighContrast ? Color.black : selected ? Enamel : Ivory;
            title.fontSize = 42 * TextScale;
            description.fontSize = 32 * TextScale;
            title.color = HighContrast ? Color.white : selected ? Ivory : Enamel;
            description.color = HighContrast ? Color.white : selected ? Ink : new Color(.14f, .21f, .19f);
            selectedMark.color = HighContrast ? Color.white : Brass;
            inlay.color = selectedMark.color;
            Size(selectedMark, 38 * TextScale, 38 * TextScale);
            selectedMark.gameObject.SetActive(selected);
        }

        public void Select() { if (museum && museum.IsActive) museum.Select(this); }
        public void ReturnToStand()
        {
            if (!museum || !museum.IsActive) return;
            transform.SetLocalPositionAndRotation(homePosition, homeRotation);
        }
    }
}
