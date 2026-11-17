using System;
using ThoraxLab.Learning;

static class InspectionZoomTests
{
    public static void Run(Action<bool, string> check)
    {
        var zoom = new InspectionZoom();
        check(zoom.Value == 1, "Inspection starts at the fitted model size.");
        zoom.Move(1, .1f);
        check(zoom.Value > 1, "Pushing up must enlarge the model.");
        zoom.Move(-1, .1f);
        check(Math.Abs(zoom.Value - 1) < .00001f, "Equal opposite stick motion should restore the model size.");
        zoom.Step(true); zoom.Step(false);
        check(Math.Abs(zoom.Value - 1) < .00001f, "Zoom panel buttons must be reversible away from a limit.");
        for (int i = 0; i < 2000; i++) zoom.Move(1, .02f);
        check(zoom.Value == InspectionZoom.Maximum, "Holding zoom in must stop at the upper size limit.");
        for (int i = 0; i < 2000; i++) zoom.Move(-1, .02f);
        check(zoom.Value == InspectionZoom.Minimum, "Holding zoom out must stop before the model disappears.");
        zoom.Reset();
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            zoom.Move(invalid, .1f); zoom.Move(1, invalid);
            check(zoom.Value == 1, "Invalid axis/time input cannot poison the model transform.");
        }
        zoom.Move(1, 0); zoom.Move(1, -1);
        check(zoom.Value == 1, "Zero or negative time cannot change the model size.");
        zoom.Move(1, 30);
        check(zoom.Value < 1.1f, "Returning after a long stalled frame must not jump to maximum zoom.");
        var fast = new InspectionZoom(); var slow = new InspectionZoom();
        for (int i = 0; i < 90; i++) fast.Move(.7f, 1f / 90);
        for (int i = 0; i < 72; i++) slow.Move(.7f, 1f / 72);
        check(Math.Abs(fast.Value - slow.Value) < .00002f, "72 Hz and 90 Hz headsets should zoom at the same rate.");
        zoom.Reset(); for (int i = 0; i < 30; i++) zoom.Step(true);
        check(zoom.Value == InspectionZoom.Maximum, "Panel zoom in respects the same limit as the controller.");
        for (int i = 0; i < 30; i++) zoom.Step(false);
        check(zoom.Value == InspectionZoom.Minimum, "Panel zoom out respects the same limit as the controller.");

        var button = new InspectionButton();
        var controller = XRInteractionSource.Controller;
        check(!button.Press(controller, true, true), "A held button at startup cannot open inspection.");
        check(!button.Press(controller, true, false) && button.Press(controller, true, true), "A fresh A/X press opens inspection.");
        check(!button.Press(controller, true, true), "Holding A/X must not toggle inspection every frame.");
        check(!button.Press(controller, true, false) && button.Press(controller, true, true), "A second fresh press can close inspection.");
        check(!button.Press(controller, false, false) && !button.Press(controller, true, true), "A menu/tracking interruption needs a release after return.");
        button.Press(controller, true, false);
        check(!button.Press(XRInteractionSource.Simulator, true, true), "Switching input source cannot replay a held inspection button.");
        check(!button.Press(XRInteractionSource.Simulator, true, false) && button.Press(XRInteractionSource.Simulator, true, true), "The simulator supports a deliberate primary-button press.");
        button.Reset();
        check(!button.Press(controller, true, true), "Changing dominant hands requires a release on the new hand.");
        check(!button.Press(XRInteractionSource.BareHand, true, false) && !button.Press(XRInteractionSource.BareHand, true, true), "Bare-hand gestures must not be mistaken for an A/X press.");

        // The runtime resets this shared stick gate whenever the ray hits UI.
        var stick = new VRStickInput(); float x = 0, y = 0;
        stick.Filter(controller, true, .2f, ref x, ref y);
        y = 1; stick.Filter(controller, true, .2f, ref x, ref y);
        check(y == 1, "A centered then pushed inspection stick can zoom.");
        stick.Reset(); y = 1; stick.Filter(controller, true, .2f, ref x, ref y);
        check(y == 0, "Moving a held scroll stick off the panel cannot immediately zoom.");
        y = 0; stick.Filter(controller, true, .2f, ref x, ref y);
        y = -1; stick.Filter(controller, true, .2f, ref x, ref y);
        check(y == -1, "Centering the stick after UI interaction restores zoom out.");
    }
}
