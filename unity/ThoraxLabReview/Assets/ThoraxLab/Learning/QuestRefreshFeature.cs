using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace ThoraxLab.Learning
{
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName="Thorax headset refresh rate",Desc="Enumerates supported refresh rates and applies explicit player choices.",Company="Anatome",Version="1.0.0",FeatureId="org.thoraxlab.refresh",OpenxrExtensionStrings="XR_FB_display_refresh_rate",BuildTargetGroups=new[]{UnityEditor.BuildTargetGroup.Android})]
#endif
    public sealed class QuestRefreshFeature : OpenXRFeature
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetProc(ulong instance,[MarshalAs(UnmanagedType.LPStr)] string name,out IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Enumerate(ulong session,uint capacity,out uint count,[Out] float[] rates);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int RequestRate(ulong session,float rate);
        Enumerate enumerate;RequestRate request;ulong session;
        public static QuestRefreshFeature Active {get;private set;}
        public float[] Supported {get;private set;}=Array.Empty<float>();
        protected override bool OnInstanceCreate(ulong instance)
        {
            if(!OpenXRRuntime.IsExtensionEnabled("XR_FB_display_refresh_rate"))return false;
            var get=Marshal.GetDelegateForFunctionPointer<GetProc>(xrGetInstanceProcAddr);
            if(get(instance,"xrEnumerateDisplayRefreshRatesFB",out var list)<0||get(instance,"xrRequestDisplayRefreshRateFB",out var set)<0)return false;
            enumerate=Marshal.GetDelegateForFunctionPointer<Enumerate>(list);request=Marshal.GetDelegateForFunctionPointer<RequestRate>(set);Active=this;return true;
        }
        protected override void OnSessionCreate(ulong handle)
        {
            session=handle;if(enumerate(session,0,out uint count,null)<0||count>16)return;var rates=new float[count];if(enumerate(session,count,out _,rates)>=0)Supported=rates;
        }
        protected override void OnSessionBegin(ulong handle){Apply(72);}
        public bool Apply(float rate)=>session!=0&&Supported.Any(r=>Math.Abs(r-rate)<.1f)&&request(session,rate)>=0;
        protected override void OnSessionDestroy(ulong handle){session=0;Supported=Array.Empty<float>();}
        protected override void OnInstanceDestroy(ulong instance){if(Active==this)Active=null;}
    }
}
