using System;

namespace ThoraxLab.Learning
{
    // All positions are metres along, across and below the training insert.
    public sealed class CaseIncisionStroke
    {
        readonly float length;
        float lastAlong, lastAcross, lastDepth;
        public float Progress { get; private set; }
        public float ContactSeconds { get; private set; }
        public bool Touching { get; private set; }
        public string Feedback { get; private set; }

        public CaseIncisionStroke(float length)
        {
            if (!(length > 0) || float.IsInfinity(length)) throw new ArgumentOutOfRangeException(nameof(length));
            this.length = length;
            Restore(false);
        }

        public void BreakContact()
        {
            Touching = false;
            Feedback = Progress > 0 ? "Touch the end of the existing mark to continue." : "Touch the beginning of the skin insert.";
        }

        public void Restore(bool completed)
        {
            Progress = completed ? 1 : 0;
            ContactSeconds = 0;
            BreakContact();
        }

        public bool Sample(float along, float across, float depth, bool aligned, bool edgeFacingSkin, float seconds)
        {
            if (!(Math.Abs(depth) <= .0025f && Math.Abs(across) <= .0035f && along >= -.0025f && along <= length + .0025f))
            { BreakContact(); return false; }
            if (!aligned || !edgeFacingSkin)
            {
                BreakContact();
                Feedback = aligned ? "Bring the cutting edge against the insert." : "Turn the blade along the mark.";
                return false;
            }
            float dx = along - lastAlong, dy = across - lastAcross, dz = depth - lastDepth;
            float maxStep = length * .60f;
            if (Touching && dx * dx + dy * dy + dz * dz > maxStep * maxStep) BreakContact();
            if (!Touching && Math.Abs(along - length * Progress) > .0025f) return false;
            if (Touching)
            {
                if (seconds > 0) ContactSeconds += Math.Min(seconds, .1f);
                float fraction = along / length;
                if (dx > 0 && fraction > Progress) Progress = fraction >= .985f ? 1 : fraction;
            }
            lastAlong = along; lastAcross = across; lastDepth = depth; Touching = true;
            Feedback = Progress == 1 ? "The skin mark is complete." : "Keep the blade edge in contact as you draw it across the insert.";
            return Progress == 1 && along >= length - .0015f;
        }
    }
}
