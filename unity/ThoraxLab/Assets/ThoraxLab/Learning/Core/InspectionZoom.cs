using System;

namespace ThoraxLab.Learning
{
    // Inspection changes only a visual copy, never the activity's metric scale.
    public sealed class InspectionZoom
    {
        public const float Minimum = .5f, Maximum = 3f;
        public float Value { get; private set; } = 1;
        public void Reset() => Value = 1;
        public void Step(bool enlarge) => Set(Value * (enlarge ? 1.25f : .8f));
        public void Move(float axis, float seconds)
        {
            if (!Finite(axis) || !Finite(seconds) || seconds <= 0) return;
            Set(Value * (float)Math.Exp(Math.Max(-1, Math.Min(1, axis)) * .9 * Math.Min(seconds, .1f)));
        }
        void Set(float value) => Value = Math.Max(Minimum, Math.Min(Maximum, value));
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // A held face button cannot reopen inspection after a menu or tracking loss.
    public sealed class InspectionButton
    {
        XRInteractionSource source;
        bool ready;
        public void Reset() { source = XRInteractionSource.None; ready = false; }
        public bool Press(XRInteractionSource next, bool available, bool held)
        {
            if (!available || (next != XRInteractionSource.Controller && next != XRInteractionSource.Simulator))
            { Reset(); return false; }
            if (source != next) { Reset(); source = next; }
            if (!held) { ready = true; return false; }
            if (!ready) return false;
            ready = false;
            return true;
        }
    }
}
