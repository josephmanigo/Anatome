using UnityEngine;
using UnityEngine.XR;

namespace ThoraxLab.Learning
{
    // Original ringless controller shell. Merged once per material; no colliders or pose smoothing.
    public sealed class ControllerVisual : MonoBehaviour
    {
        readonly WorkbenchGeometry shell = new();
        readonly WorkbenchGeometry controls = new();
        Transform trigger;
        XRNode hand;
        public void Initialize(XRNode side,Material body,Material face,Material accent)
        {
            hand=side;float mirror=side==XRNode.LeftHand?-1:1;
            shell.Ellipsoid(new Vector3(0,-.040f,-.018f),new Vector3(.041f,.112f,.046f),body,Quaternion.Euler(15,0,0),24,16);
            shell.Ellipsoid(new Vector3(0,.014f,.010f),new Vector3(.069f,.037f,.080f),body,segments:28,rings:16);
            shell.Ellipsoid(new Vector3(0,.029f,.009f),new Vector3(.058f,.010f,.068f),face,segments:28);
            shell.Ellipsoid(new Vector3(mirror*.020f,-.025f,-.007f),new Vector3(.004f,.036f,.019f),face,Quaternion.Euler(15,0,0));
            shell.Ellipsoid(new Vector3(-mirror*.013f,.038f,-.004f),new Vector3(.010f,.013f,.010f),body);
            shell.Ellipsoid(new Vector3(-mirror*.013f,.044f,-.004f),new Vector3(.018f,.006f,.018f),face);
            foreach(float z in new[]{-.005f,.015f})shell.Ellipsoid(new Vector3(mirror*.012f,.037f,z),new Vector3(.011f,.005f,.011f),body);
            shell.Ellipsoid(new Vector3(-mirror*.010f,.035f,.021f),new Vector3(.007f,.003f,.007f),body);
            shell.Box(new Vector3(0,-.073f,-.041f),new Vector3(.013f,.0025f,.0015f),.001f,accent,Quaternion.Euler(15,0,0));
            shell.Build(transform,"Smooth controller shell");
            trigger=new GameObject("Index trigger").transform;trigger.SetParent(transform,false);trigger.localPosition=new Vector3(0,.001f,.045f);
            controls.Ellipsoid(Vector3.zero,new Vector3(.029f,.023f,.015f),face,Quaternion.Euler(-20,0,0));
            controls.Build(trigger,"Index trigger surface");
        }
        void LateUpdate(){if(trigger)trigger.localRotation=Quaternion.Euler(LearningXRInput.Read(hand).trigger?-12:0,0,0);}
        void OnDestroy(){shell.Dispose();controls.Dispose();}
    }
}
