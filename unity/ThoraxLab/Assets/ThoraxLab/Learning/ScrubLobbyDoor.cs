using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class ScrubLobbyDoor : MonoBehaviour
    {
        public Transform southLeaf, northLeaf;
        Camera viewer;
        float fraction;

        void Update()
        {
            if (!viewer || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (!viewer || !southLeaf || !northLeaf) return;
            var eye = transform.InverseTransformPoint(viewer.transform.position);
            // The sensor spans both sides of the opening and the full clear door width.
            bool occupied = Mathf.Abs(eye.x - 5.99f) < 1.72f && Mathf.Abs(eye.z - 2.52f) < 1.27f;
            float next = Mathf.MoveTowards(fraction, occupied ? 1f : 0f, Time.unscaledDeltaTime * 1.65f);
            if (next == fraction) return;
            fraction = next;
            var travel = Vector3.forward * (1.075f * Mathf.SmoothStep(0, 1, fraction));
            southLeaf.localPosition = travel;
            northLeaf.localPosition = -travel;
        }
    }
}
