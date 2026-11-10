using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace ThoraxLab.Learning
{
    // Orientation is a constrained handling exercise, not a dissection. A wrist
    // turn must never rotate the whole chest around a distant controller pivot.
    public sealed class OrientationGrabConstraint : XRBaseGrabTransformer
    {
        Vector3 handStart, bodyStart;
        Quaternion bodyRotation;
        public override bool canProcess => base.canProcess && LabRuntime.Instance && LabRuntime.Instance.OrientationActive;
        public override void OnGrab(XRGrabInteractable grab)
        {
            bodyStart = grab.transform.position;
            bodyRotation = grab.transform.rotation;
            if (grab.interactorsSelecting.Count > 0) handStart = grab.interactorsSelecting[0].GetAttachTransform(grab).position;
        }
        public override void Process(XRGrabInteractable grab, XRInteractionUpdateOrder.UpdatePhase phase, ref Pose pose, ref Vector3 scale)
        {
            if (grab.interactorsSelecting.Count == 0) return;
            var displacement = grab.interactorsSelecting[0].GetAttachTransform(grab).position - handStart;
            pose.position = bodyStart + Vector3.up * Mathf.Clamp(displacement.y, 0, .12f);
            pose.rotation = bodyRotation;
        }
    }
}
