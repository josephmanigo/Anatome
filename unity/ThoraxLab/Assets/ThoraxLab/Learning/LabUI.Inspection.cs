using TMPro;
using UnityEngine;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        TextMeshProUGUI inspectionZoom, inspectionToggle;
        RectTransform inspectionButtons;
        string InspectionShortcut => app.Settings.Current.leftHanded ? "X on the left controller" : "A on the right controller";
        string InspectionInstructions => "Press " + InspectionShortcut + " for zoom controls. Point away from the panel. Move that stick up or down to zoom, or sideways to rotate the view. Use trigger and grip to keep working. Press the same button to return to normal size.";

        void InspectionControls()
        {
            var toggle = Button(content, app.MuseumActive ? "Inspect / zoom selected exhibit" : "Inspect / zoom model", app.Inspector.Toggle);
            inspectionToggle = toggle.GetComponentInChildren<TextMeshProUGUI>();
            inspectionButtons = Column(content, "Model zoom controls");
            inspectionZoom = Text(inspectionButtons, "", 24, Accent);
            var row = Row(inspectionButtons, "Model zoom", 8);
            Button(row, "Zoom out −", app.Inspector.ZoomOut);
            Button(row, "Zoom in +", app.Inspector.ZoomIn);
            Button(row, "Reset view", app.Inspector.ResetView);
            RefreshInspectionZoom();
        }

        public void RefreshInspectionZoom()
        {
            if (!inspectionButtons || !app.Inspector) return;
            bool open = app.Inspector.IsOpen;
            if (inspectionButtons.gameObject.activeSelf != open) inspectionButtons.gameObject.SetActive(open);
            inspectionToggle.text = open ? "Close zoom" : app.MuseumActive ? "Inspect / zoom selected exhibit" : "Inspect / zoom model";
            if (open) inspectionZoom.SetText("Model zoom {0:0}%", Mathf.Round(app.Inspector.Magnification * 100));
        }
    }
}
