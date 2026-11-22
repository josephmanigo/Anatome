using System;
using UnityEngine;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        const string AssessmentRequirements = "Score at least 80%, complete every essential step, and avoid critical safety errors. Complete the assessment without answer hints.";

        void LearningGuidelines(Transform parent)
        {
            Text(parent,"80% minimum passing score",28,Accent);
            Text(parent,AssessmentRequirements,24);
            Text(parent,"Easy → Medium → Hard",27);
            Text(parent,"Complete the VR controls introduction to open Easy. Pass the Easy assessment to unlock Medium, then pass Medium to unlock Hard in the same unit and mode.",24);
            Text(parent,"Lessons and practice help you prepare. They and XP do not unlock levels. There is no time limit, and you can retry assessments as often as you need.",22,Muted);
        }

        void ShowLearningGuidelines(Action back)
        {
            Page("Getting started","Before you begin",back:back);
            LearningGuidelines(content);
            Rule(content);
            Text(content,"Point and press trigger to select. Grip to hold a movable part; release to let go. Follow the current instruction beside the specimen, then choose Finish assessment to review your result.",24);
            Text(content,"In every activity, enlarge the 3D model for a closer look. "+InspectionInstructions,24);
            Text(content,"Visit the Thorax Museum to explore labeled exhibits grouped by body system. Every part has a name and brief description; museum visits are ungraded and also support grab and zoom.",24);
            Text(content,"Use Guide for controls, or Menu to pause. You can adjust comfort and text size at any time.",22,Muted);
            Button(content,"VR comfort & text size",()=>OpenAccessibilitySetup(()=>ShowLearningGuidelines(back)));
        }

        void OpenAccessibilitySetup(Action back)
        {
            previousScreen=back;
            settingCategory="Quick setup";
            ShowSettings();
        }

        string MovementSummary()
        {
            var settings=app.Settings.Current;
            string movement=settings.leftHanded?"Right":"Left",turning=settings.leftHanded?"Left":"Right";
            return (settings.movement==MoveStyle.Smooth?movement+" stick walks. ":movement+" stick forward, then center to teleport. ")+
                (settings.turning==TurnStyle.Snap?turning+" stick sideways turns once; center it before another turn. ":turning+" stick sideways turns smoothly. ")+
                "Center both sticks after pausing or using the panel.";
        }
    }
}
