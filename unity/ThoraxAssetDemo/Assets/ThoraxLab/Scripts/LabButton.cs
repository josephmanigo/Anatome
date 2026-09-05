using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ThoraxLab {
public class LabButton:MonoBehaviour {public string action;public DissectionSession session;public void Invoke(){switch(action){case "Previous":session.Browse(-1);break;case "Browse":session.Browse(1);break;case "Systems":session.CycleSystem();break;case "Height +":session.SetTableHeight(session.specimen.position.y+.05f);break;case "Height -":session.SetTableHeight(session.specimen.position.y-.05f);break;case "Open chest":session.OpenChest();break;case "Explore":session.SetMode(LabMode.Explore);break;case "Guided":session.SetMode(LabMode.Guided);break;case "Free":session.SetMode(LabMode.Free);break;case "Next":session.GuidedNext();break;case "Reset":session.ResetSpecimen();break;case "Undo":session.Undo();break;case "Hide":session.HideSelected();break;case "Isolate":session.Isolate();break;case "Return":session.ReturnSelected();break;case "Scalpel":session.saw=false;break;case "Saw":session.saw=true;break;default:session.ShowSystem(action);break;}}}
}
