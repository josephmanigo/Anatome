using System;

namespace ThoraxLab.Learning
{
    // Require a centered stick after an interruption, then ease out of the
    // deadzone without a sudden jump to the minimum walking speed.
    public sealed class VRStickInput
    {
        XRInteractionSource source;
        public bool Ready { get; private set; }
        public bool Interrupted { get; private set; }

        public void Reset() { source = XRInteractionSource.None; Ready = false; Interrupted = true; }

        public void Filter(XRInteractionSource next, bool tracked, float deadzone, ref float x, ref float y)
        {
            Interrupted = false;
            if (!tracked || next == XRInteractionSource.None || !Finite(x) || !Finite(y) ||
                !Finite(deadzone) || deadzone <= 0 || deadzone >= 1)
            { Reset(); x = y = 0; return; }
            if (source != next) { Reset(); source = next; }
            double length = Math.Sqrt((double)x * x + (double)y * y);
            if (length <= deadzone) { Ready = true; x = y = 0; return; }
            if (!Ready) { x = y = 0; return; }
            float scale = (float)((Math.Min(length, 1) - deadzone) / (1 - deadzone) / length);
            x *= scale; y *= scale;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class SnapTurnLatch
    {
        bool ready;
        public void Reset() => ready = false;
        public int Read(float x, float y)
        {
            if (x == 0 && y == 0) { ready = true; return 0; }
            if (!ready || Math.Abs(x) < .7f || Math.Abs(x) <= Math.Abs(y)) return 0;
            ready = false;
            return x > 0 ? 1 : -1;
        }
    }
}
