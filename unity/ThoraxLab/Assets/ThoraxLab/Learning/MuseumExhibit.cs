using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ThoraxLab.Learning
{
    // Museum copies are grabbable displays, never AnatomyStructure assessment targets.
    public sealed class MuseumExhibit : MonoBehaviour
    {
        ThoraxMuseum museum;
        Vector3 homePosition;
        Quaternion homeRotation;
        public MuseumEntry Entry { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public RectTransform Label { get; set; }

        public void Initialize(ThoraxMuseum owner, MuseumEntry entry)
        {
            museum = owner; Entry = entry; Renderer = GetComponent<MeshRenderer>();
            homePosition = transform.localPosition; homeRotation = transform.localRotation;
            var body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.movementType = XRBaseInteractable.MovementType.Instantaneous; Grab.throwOnDetach = false;
            Grab.useDynamicAttach = true; Grab.matchAttachPosition = true; Grab.matchAttachRotation = true;
            Grab.snapToColliderVolume = false; Grab.unparentTransformOnGrab = false;
            Grab.selectEntered.AddListener(_ => Select());
            Grab.selectExited.AddListener(_ => ReturnToStand());
        }
        public void Select() { if (museum && museum.IsActive) museum.Select(this); }
        public void ReturnToStand()
        {
            if (!museum || !museum.IsActive) return;
            transform.SetLocalPositionAndRotation(homePosition, homeRotation);
        }
    }
}
