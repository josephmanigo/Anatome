using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ThoraxLab.Learning
{
    // Avatar equipment only: the tracked interactor, its grip origin and the HMD
    // remain authoritative. These skin bones never participate in assessment physics.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LabHandInteractor))]
    public sealed class GlovedHandVisual : MonoBehaviour
    {
        sealed class Joint
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 axis;
            public float degrees;
            public int finger;
            public Vector3 oppositionAxis;
            public float oppositionDegrees;
        }
        readonly List<Joint> joints=new List<Joint>(16);
        LabHandInteractor interactor;
        LineRenderer pointer;
        Transform indexTip;
        public XRNode Hand => interactor ? interactor.hand : XRNode.RightHand;
        public Transform VisualRoot { get; private set; }
        public SkinnedMeshRenderer Skin { get; private set; }
        public Transform[] Fingers { get; private set; } = Array.Empty<Transform>();
        public float GripCurl { get; private set; }
        public float IndexCurl { get; private set; }
        public bool Visible => VisualRoot && VisualRoot.gameObject.activeInHierarchy && Skin && Skin.enabled;

        void Start()
        {
            interactor=GetComponent<LabHandInteractor>();pointer=GetComponent<LineRenderer>();
            var source=Resources.Load<GameObject>("Learning/Hands/"+(Hand==XRNode.LeftHand?"GloveLeft":"GloveRight"));
            if(!source){Debug.LogError("Bundled gloved hand is missing. Run GlovedHandAssets.Prepare before opening the learning scene.");enabled=false;return;}
            VisualRoot=Instantiate(source,transform,false).transform;
            VisualRoot.name=Hand==XRNode.LeftHand?"Left gloved hand":"Right gloved hand";
            Skin=VisualRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if(!Skin){Debug.LogError("The gloved hand prefab has no skinned hand mesh.");enabled=false;return;}
            string side=Hand==XRNode.LeftHand?"L_":"R_";
            var nodes=new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach(var node in VisualRoot.GetComponentsInChildren<Transform>(true))nodes[node.name]=node;
            string[] digits={"Thumb","Index","Middle","Ring","Little"};Fingers=new Transform[5];
            for(int finger=0;finger<digits.Length;finger++)
            {
                string digit=digits[finger];
                Fingers[finger]=nodes[side+digit+"Proximal"];
                if(finger==0)
                {
                    Bind(nodes[side+digit+"Metacarpal"],nodes[side+digit+"Proximal"],finger,12);
                    var thumb=joints[joints.Count-1];
                    // Oppose the thumb toward the index as the hand closes. Flexion
                    // alone makes it hook below the fist instead of wrapping a grip.
                    thumb.oppositionAxis=thumb.bone.InverseTransformDirection(VisualRoot.up*(Hand==XRNode.RightHand?1:-1)).normalized;
                    thumb.oppositionDegrees=20;
                }
                Bind(nodes[side+digit+"Proximal"],nodes[side+digit+(finger==0?"Distal":"Intermediate")],finger,finger==0?24:62);
                if(finger!=0)Bind(nodes[side+digit+"Intermediate"],nodes[side+digit+"Distal"],finger,78);
                Bind(nodes[side+digit+"Distal"],nodes[side+digit+"Tip"],finger,finger==0?18:48);
            }
            indexTip=nodes[side+"IndexTip"];
            VisualRoot.gameObject.SetActive(false);
        }
        void Bind(Transform bone,Transform child,int finger,float degrees)
        {
            var toward=(child.position-bone.position).normalized;
            var worldAxis=Vector3.Cross(toward,-VisualRoot.up).normalized;
            if(worldAxis.sqrMagnitude<.5f)worldAxis=VisualRoot.right;
            joints.Add(new Joint{bone=bone,rest=bone.localRotation,axis=bone.InverseTransformDirection(worldAxis).normalized,degrees=degrees,finger=finger});
        }
        void LateUpdate()
        {
            if(!VisualRoot||!Skin)return;
            var input=LearningXRInput.Read(Hand);
            bool tracked=input.tracked&&LearningXRInput.HeadTracked;
            if(VisualRoot.gameObject.activeSelf!=tracked)VisualRoot.gameObject.SetActive(tracked);
            if(!tracked)
            {
                if(GripCurl!=0||IndexCurl!=0)
                {
                    GripCurl=IndexCurl=0;
                    foreach(var joint in joints)joint.bone.localRotation=joint.rest;
                }
                if(pointer)pointer.enabled=false;
                return;
            }
            float grip=input.gripValue;
            if(interactor.interactablesSelected.Count>0)grip=Mathf.Max(grip,.86f);
            if(interactor.session&&interactor.session.mode==LabMode.Free&&LabRuntime.Instance&&LabRuntime.Instance.CanUseCuttingTool)grip=Mathf.Max(grip,.74f);
            float nextGrip=Mathf.MoveTowards(GripCurl,grip,Time.unscaledDeltaTime*9);
            float nextIndex=Mathf.MoveTowards(IndexCurl,Mathf.Max(input.triggerValue,grip*.9f),Time.unscaledDeltaTime*9);
            if(nextGrip!=GripCurl||nextIndex!=IndexCurl)
            {
                GripCurl=nextGrip;
                IndexCurl=nextIndex;
                ApplyCurl();
            }
            UpdatePointer();
        }
        void ApplyCurl()
        {
            foreach(var joint in joints)
            {
                float curl=joint.finger==1?IndexCurl:GripCurl;
                var bend=Quaternion.AngleAxis(joint.degrees*curl,joint.axis);
                if(joint.oppositionDegrees>0)bend=Quaternion.AngleAxis(joint.oppositionDegrees*curl,joint.oppositionAxis)*bend;
                joint.bone.localRotation=joint.rest*bend;
            }
        }
        void UpdatePointer()
        {
            if(!pointer)return;
            var app=LabRuntime.Instance;var ray=new Ray(transform.position,transform.forward);
            // Keep the same input ray. Only its visual start follows the index fingertip.
            bool show=false;Vector3 endpoint=ray.GetPoint(2);
            if(app&&app.UI)
            {
                var panel=app.UI.Root;var plane=new Plane(panel.forward,panel.position);
                if(plane.Raycast(ray,out float distance)&&distance>0&&distance<3)
                {
                    var local=panel.InverseTransformPoint(ray.GetPoint(distance));
                    if(panel.rect.Contains(new Vector2(local.x,local.y))){show=true;endpoint=ray.GetPoint(distance);}
                }
            }
            if(!show&&Physics.Raycast(ray,out var hit,3))
            {
                show=hit.collider.GetComponentInParent<AnatomyStructure>()||hit.collider.GetComponentInParent<ProcedureTool>()||hit.collider.GetComponent<LabButton>();
                endpoint=hit.point;
            }
            pointer.enabled=show&&interactor.interactablesSelected.Count==0;
            if(pointer.enabled){pointer.SetPosition(0,indexTip.position);pointer.SetPosition(1,endpoint);}
        }
        void OnDisable(){if(VisualRoot)VisualRoot.gameObject.SetActive(false);if(pointer)pointer.enabled=false;}
    }
}
