// Only for the standalone regression harness. The shipping LearningControls.cs
// is compiled unmodified; XR timing and transform translation are modeled here.
using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object o)=>o!=null; public static T FindFirstObjectByType<T>() where T:class=>null; }
 public class Component:Object { public GameObject gameObject=new GameObject(); public Transform transform=>gameObject.transform; }
 public class MonoBehaviour:Component { public bool enabled=true; }
 public class GameObject:Object { public Transform transform=new Transform(); public T AddComponent<T>() where T:new()=>new T(); }
 public class Transform:Object {
  public Transform parent; public Quaternion rotation=Quaternion.identity,localRotation=Quaternion.identity; public Vector3 localPosition;
  public Vector3 position {get=>(parent?parent.position:Vector3.zero)+localPosition;set=>localPosition=value-(parent?parent.position:Vector3.zero);}
  public Vector3 forward=>Vector3.forward;
  public Vector3 InverseTransformPoint(Vector3 world)=>world-position;
  public T GetComponent<T>() where T:class=>null;
  public void SetLocalPositionAndRotation(Vector3 p,Quaternion q){localPosition=p;localRotation=q;}
  public void RotateAround(Vector3 p,Vector3 axis,float angle){}
 }
 public class Camera:Component {public static Camera main;public float fieldOfView=58;}
 public class CharacterController:Component {public bool enabled=true;public float radius=.18f,skinWidth=.08f,height,stepOffset;public Vector3 center;public void Move(Vector3 delta){transform.position+=delta;}}
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new(0,0,0);public static Vector3 up=>new(0,1,0);public static Vector3 down=>new(0,-1,0);public static Vector3 forward=>new(0,0,1);public static Vector3 right=>new(1,0,0);
  public float magnitude=>MathF.Sqrt(x*x+y*y+z*z);public float sqrMagnitude=>x*x+y*y+z*z;public Vector3 normalized=>magnitude>0?this*(1/magnitude):zero;
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float f)=>new(a.x*f,a.y*f,a.z*f);
  public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n)=>new(a.x,0,a.z);
  public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
 }
 public struct Vector2 {public float x,y;public float magnitude=>MathF.Sqrt(x*x+y*y);public static Vector2 zero=>default;public static Vector2 ClampMagnitude(Vector2 v,float max){float length=v.magnitude;return length>max?new Vector2{x=v.x*max/length,y=v.y*max/length}:v;}}
 public struct Quaternion {public static Quaternion identity=>new();public static Quaternion Euler(float x,float y,float z)=>new();public static Vector3 operator *(Quaternion q,Vector3 v)=>v;}
 public static class Mathf {public const float Rad2Deg=180/MathF.PI;public static float Atan2(float y,float x)=>MathF.Atan2(y,x);public static float Max(float a,float b)=>MathF.Max(a,b);public static float Abs(float v)=>MathF.Abs(v);public static float Sign(float v)=>MathF.Sign(v);public static bool Approximately(float a,float b)=>Abs(a-b)<.0001f;}
 public static class Time {public static float deltaTime=1f/72f;public static float unscaledTime;public static int frameCount;}
 public struct Ray {public Vector3 origin;public Ray(Vector3 a,Vector3 b){origin=a;}}
 public struct RaycastHit {public Vector3 point,normal;}
 public static class Physics {public static int raycasts;public static Ray lastRay;public static bool Raycast(Ray r,out RaycastHit hit,float d){raycasts++;lastRay=r;hit=default;return false;}}
 public static class SubsystemManager {public static readonly List<XR.XRInputSubsystem> Systems=new();public static void GetSubsystems(List<XR.XRInputSubsystem> systems){systems.Clear();systems.AddRange(Systems);}}
 public static class Debug {public static void Log(object o)=>Console.WriteLine(o);public static void LogWarning(object o)=>Console.WriteLine(o);}
}
namespace UnityEngine.XR {
 public enum XRNode {Head,LeftHand,RightHand}
 [Flags] public enum TrackingOriginModeFlags {Unknown=0,Device=1,Floor=2,TrackingReference=4,Unbounded=8}
 public class XRInputSubsystem {
  public bool running=true,acceptRequests=true;public TrackingOriginModeFlags mode=TrackingOriginModeFlags.Floor,supported=TrackingOriginModeFlags.Floor|TrackingOriginModeFlags.Device,requested;
  public event Action<XRInputSubsystem> trackingOriginUpdated;
  public bool TrySetTrackingOriginMode(TrackingOriginModeFlags value){requested=value;return acceptRequests;}
  public TrackingOriginModeFlags GetTrackingOriginMode()=>mode;
  public TrackingOriginModeFlags GetSupportedTrackingOriginModes()=>supported;
  public bool TryRecenter(){trackingOriginUpdated?.Invoke(this);return true;}
  public void CompleteOriginChange(TrackingOriginModeFlags value){mode=value;trackingOriginUpdated?.Invoke(this);}
 }
}
namespace ThoraxLab {public class LabControls:UnityEngine.MonoBehaviour{}}
namespace ThoraxLab.Learning {
 public class ComfortVignette:UnityEngine.Component {public void SetMoving(bool value){}}
 public class SettingsService {public SettingsProfile Current=new();}
 public class LabUI:UnityEngine.Object {public bool XRPanelCaptured;public int recenterCount,menuCount;public void RecenterPanel(){recenterCount++;}public void TogglePauseMenu(){menuCount++;}}
 public class LabRuntime {public LabUI UI=new();public SettingsService Settings=new();public bool CanInteract=false;public bool Focused=true;}
 public static class LearningXRInput {
  public struct State {public bool tracked,menu;public UnityEngine.Vector3 position;public UnityEngine.Quaternion rotation;public UnityEngine.Vector2 axis;public XRInteractionSource source;}
  public static bool IsSimulated;public static State head,left,right;
  public static State Read(UnityEngine.XR.XRNode node)=>node==UnityEngine.XR.XRNode.Head?head:node==UnityEngine.XR.XRNode.LeftHand?left:right;
 }
}
