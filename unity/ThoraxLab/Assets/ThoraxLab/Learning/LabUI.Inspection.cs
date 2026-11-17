using TMPro;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        TextMeshProUGUI inspectionZoom;
        string InspectionShortcut => app.Settings.Current.leftHanded ? "X on the left controller" : "A on the right controller";
        string InspectionInstructions => "Press " + InspectionShortcut + " to inspect the model. Point away from the panel and push that stick up to zoom in, down to zoom out, or sideways to rotate. Press the same button to return to your activity.";

        public void ShowModelInspection()
        {
            Page("Model inspection", "Inspect the 3D model");
            if(app.MuseumActive)RecenterPanel();
            Button(content, "Return to activity", app.Inspector.ReturnToActivity, true);
            inspectionZoom = Text(content, "", 30, Accent);
            var buttons = Row(content, "Model zoom", 12);
            Button(buttons, "Zoom out −", app.Inspector.ZoomOut);
            Button(buttons, "Zoom in +", app.Inspector.ZoomIn);
            Button(content, "Reset view", app.Inspector.ResetView);
            if(app.MuseumActive&&app.Museum.Selected)
            {
                Text(content,app.Museum.Selected.Entry.Name,27,Accent);
                Text(content,app.Museum.Selected.Entry.Description,24);
            }
            Text(content, "Point away from the panel. Push the " + (app.Settings.Current.leftHanded ? "left" : "right") + " stick up to zoom in, down to zoom out, or sideways to rotate. Press " + InspectionShortcut + " to return to your activity.", 24);
            Text(content, app.MuseumActive ? "Movement is paused while you inspect. Return to the gallery to select or hold another exhibit." : "Activity interactions and movement are paused while you inspect. Your current step, cuts and progress are kept.", 22, Muted);
            Text(content, "Zoom range: 50%–300%. Point at this panel to scroll instead. Center the stick before zooming again.", 22, Muted);
            RefreshInspectionZoom();
        }

        public void RefreshInspectionZoom()
        {
            if (inspectionZoom && app.Inspector)
                inspectionZoom.text = "Model size: " + (app.Inspector.Magnification * 100).ToString("0") + "%";
        }
    }
}
