using System;
using System.Reflection;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.XR;

static class Program {
 static int checks;
 static void Check(string name,bool pass){Console.WriteLine((pass?"PASS: ":"FAIL: ")+name);if(!pass)throw new Exception(name);checks++;}
 static void Height(string name,float expected)=>Check(name,MathF.Abs(Camera.main.transform.position.y-expected)<.01f);
 static void Destroy(LearningControls controls)=>typeof(LearningControls).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(controls,null);
 static (LearningControls controls,LabRuntime app,XRInputSubsystem system) Create(bool seated,TrackingOriginModeFlags mode,bool simulated=false,bool running=true,bool supportFloor=true,bool acceptRequests=true,bool autoHeight=false){
  SubsystemManager.Systems.Clear();Time.frameCount=0;Time.unscaledTime=0;LearningXRInput.IsSimulated=simulated;
  var system=new XRInputSubsystem{mode=mode,running=running,supported=supportFloor?TrackingOriginModeFlags.Floor|TrackingOriginModeFlags.Device:TrackingOriginModeFlags.Device,acceptRequests=acceptRequests};if(!simulated)SubsystemManager.Systems.Add(system);
  Camera.main=new Camera();Camera.main.transform.parent=new Transform();
  var app=new LabRuntime();app.Settings.Current.seated=seated;app.Settings.Current.autoHeight=autoHeight;
  var controls=new LearningControls();controls.Initialize(app);return(controls,app,system);
 }
 static void Tick(LearningControls controls,float height,bool tracked=true) {
  Time.frameCount++;Time.unscaledTime+=Time.deltaTime;
  LearningXRInput.head=new(){tracked=tracked,position=new Vector3(0,height,0),rotation=Quaternion.identity};
  typeof(LearningControls).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controls,null);
 }
 static int Main() {
  var preview=Create(false,TrackingOriginModeFlags.Floor,simulated:true);preview.app.Settings.Current.simulatorFov=95;Tick(preview.controls,1.65f);
  Check("simulator startup uses saved FOV",Camera.main.fieldOfView==95);Destroy(preview.controls);
  var headset=Create(false,TrackingOriginModeFlags.Floor);headset.app.Settings.Current.simulatorFov=95;Camera.main.fieldOfView=84;Tick(headset.controls,1.65f);
  Check("headset startup preserves runtime FOV",Camera.main.fieldOfView==84);Destroy(headset.controls);
  Check("height assistance enabled by default",new SettingsProfile().autoHeight);
  var roomScale=Create(false,TrackingOriginModeFlags.Floor,autoHeight:true);
  Time.frameCount++;LearningXRInput.head=new(){tracked=true,position=new Vector3(1.2f,1.65f,-.8f),rotation=Quaternion.identity};
  typeof(LearningControls).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(roomScale.controls,null);
  Check("room-scale offset still starts the headset in the clear aisle",MathF.Abs(Camera.main.transform.position.x-.85f)<.001f&&MathF.Abs(Camera.main.transform.position.z-.65f)<.001f);
  Destroy(roomScale.controls);
  var shortPlayer=Create(true,TrackingOriginModeFlags.Floor,autoHeight:true);
  Tick(shortPlayer.controls,1.22f);
  Height("short player can see above the work surface at startup",1.65f);
  Tick(shortPlayer.controls,1.02f);Height("height assistance preserves crouching",1.45f);
  Tick(shortPlayer.controls,1.02f,false);Tick(shortPlayer.controls,1.02f);
  Height("tracking recovery does not erase crouching",1.45f);
  shortPlayer.app.Settings.Current.heightBoost=.3f;shortPlayer.controls.ApplyComfort();
  Height("manual boost raises the rig without erasing crouch",1.75f);
  shortPlayer.controls.ApplyComfort();Height("unrelated settings do not double the boost",1.75f);
  shortPlayer.controls.RecenterTracking();Tick(shortPlayer.controls,1.22f);Height("recenter retains manual boost",1.95f);
  shortPlayer.app.Settings.Current.autoHeight=false;shortPlayer.controls.ApplyComfort();Height("assistance can be disabled",1.52f);
  shortPlayer.app.Settings.Current.heightBoost=0;shortPlayer.controls.ApplyComfort();Height("zero boost restores measured height",1.22f);
  Destroy(shortPlayer.controls);
  var tallPlayer=Create(false,TrackingOriginModeFlags.Floor,autoHeight:true);Tick(tallPlayer.controls,1.85f);Height("tall player is not lowered or raised",1.85f);Destroy(tallPlayer.controls);
  var devicePlayer=Create(true,TrackingOriginModeFlags.Device,supportFloor:false,autoHeight:true);Tick(devicePlayer.controls,0);Height("Device origin supports seated height assistance",1.65f);Destroy(devicePlayer.controls);
  var delayedShortPlayer=Create(true,TrackingOriginModeFlags.Floor,autoHeight:true);Tick(delayedShortPlayer.controls,0);Tick(delayedShortPlayer.controls,.9f);Height("late short floor pose does not double height assistance",1.65f);Destroy(delayedShortPlayer.controls);
  var boostedPlayer=Create(true,TrackingOriginModeFlags.Floor,autoHeight:true);boostedPlayer.app.Settings.Current.heightBoost=.8f;boostedPlayer.app.CanInteract=true;
  boostedPlayer.app.Settings.Current.movement=MoveStyle.Teleport;
  LearningXRInput.left=new(){tracked=true,source=XRInteractionSource.Controller};Tick(boostedPlayer.controls,.9f);
  LearningXRInput.left.axis=new(){y=1};Tick(boostedPlayer.controls,.9f);LearningXRInput.left.axis=default;Tick(boostedPlayer.controls,.9f);
  Check("teleport floor probe still reaches ground with maximum height boost",Physics.lastRay.origin.y>=0&&Physics.lastRay.origin.y<=3);
  Destroy(boostedPlayer.controls);
  var profile=new SettingsProfile{heightBoost=5};profile.Validate();Check("height boost is bounded",profile.heightBoost==.8f);profile.heightBoost=-1;profile.Validate();Check("height boost cannot lower player below tracked floor",profile.heightBoost==0);
  var (controls,app,subsystem)=Create(true,TrackingOriginModeFlags.Floor);
  Tick(controls,1.65f);
  Height("real floor height is preserved even with seated preference",1.65f);
  Check("physical headset projection is not overridden",Camera.main.fieldOfView==58);
  Console.WriteLine($"Startup Floor pose: eye={Camera.main.transform.position.y:F2}m; requested={subsystem.requested}");
  subsystem.CompleteOriginChange(TrackingOriginModeFlags.Device);
  Tick(controls,0);Tick(controls,0);
  float actual=Camera.main.transform.position.y;
  Console.WriteLine($"Delayed Device pose: eye={actual:F2}m; expected=1.25m");
  if(MathF.Abs(actual-1.25f)>.05f){Console.WriteLine("FAIL: startup origin change leaves POV under the floor");return 1;}
  Height("startup origin transition keeps POV at seated eye height",1.25f);
  Check("layout never requests Device mode",subsystem.requested!=TrackingOriginModeFlags.Device);
  Tick(controls,-.2f);Height("device-relative crouch preserved",1.05f);
  controls.ApplyComfort();Height("unrelated settings do not reset crouch",1.05f);
  controls.RecenterTracking();Tick(controls,0);Height("manual recenter calibrates fresh pose",1.25f);
  subsystem.CompleteOriginChange(TrackingOriginModeFlags.Floor);
  Tick(controls,0);Tick(controls,1.65f);Height("late floor pose adopts measured eye height",1.65f);
  Tick(controls,1.45f);Height("floor-relative crouch preserved",1.45f);
  app.Settings.Current.seated=false;controls.ApplyComfort();Height("standing uses reported floor height",1.45f);
  app.Settings.Current.seated=true;controls.ApplyComfort();Height("seated preference does not shrink the learner",1.45f);
  Tick(controls,1.45f,false);Height("tracking loss holds last valid camera pose",1.45f);
  subsystem.CompleteOriginChange(TrackingOriginModeFlags.Device);Tick(controls,0);Tick(controls,0);Height("tracking recovery after origin change",1.25f);
  Tick(controls,float.NaN);Height("invalid height cannot corrupt rig transform",1.25f);
  Tick(controls,0);Height("valid pose recovers after invalid sample",1.25f);
  Destroy(controls);

  (controls,app,subsystem)=Create(false,TrackingOriginModeFlags.Device,supportFloor:false);
  Tick(controls,0);Height("standing Device fallback starts above floor",1.65f);
  Check("unsupported floor mode is not requested",subsystem.requested==TrackingOriginModeFlags.Unknown);
  Tick(controls,-.25f);Height("standing Device fallback preserves head movement",1.4f);Destroy(controls);

  (controls,app,subsystem)=Create(false,TrackingOriginModeFlags.Device,acceptRequests:false);
  Tick(controls,0);Height("rejected floor request still has safe eye height",1.65f);
  Check("floor requested for supported runtime",subsystem.requested==TrackingOriginModeFlags.Floor);
  subsystem.CompleteOriginChange(TrackingOriginModeFlags.Floor);Tick(controls,0);Tick(controls,1.72f);
  Height("standing delayed floor pose does not double eye height",1.72f);Destroy(controls);

  (controls,app,subsystem)=Create(true,TrackingOriginModeFlags.Floor,true);
  Tick(controls,1.65f);Height("official simulator floor pose is not double offset",1.65f);
  Check("only simulator preview uses a wider 75 degree lens",Camera.main.fieldOfView==75);
  Tick(controls,0);Height("late pose without event recovers from underfloor jump",1.25f);
  Tick(controls,1.65f);Height("reverse delayed pose without event recovers from high jump",1.65f);
  Tick(controls,1.45f);Height("ordinary crouching stays available after recovery",1.45f);Destroy(controls);

  (controls,app,subsystem)=Create(false,TrackingOriginModeFlags.Unknown,false,false);
  Tick(controls,0);Height("subsystem not ready at startup has safe fallback",1.65f);
  subsystem.running=true;subsystem.CompleteOriginChange(TrackingOriginModeFlags.Floor);
  Tick(controls,1.7f);Tick(controls,1.7f);Height("late subsystem startup adopts actual floor",1.7f);
  Destroy(controls);

  (controls,app,subsystem)=Create(true,TrackingOriginModeFlags.Unknown,false,false);
  Tick(controls,0,false);subsystem.running=true;subsystem.CompleteOriginChange(TrackingOriginModeFlags.Device);
  Tick(controls,0);Height("first tracked Device pose is safe on the same frame as subsystem startup",1.25f);
  subsystem.CompleteOriginChange(TrackingOriginModeFlags.Floor);Tick(controls,0);Height("origin transition holds last safe view for the change frame",1.25f);
  Tick(controls,1.68f);Height("fresh floor pose completes startup without double height",1.68f);
  Destroy(controls);

  (controls,app,subsystem)=Create(false,TrackingOriginModeFlags.Floor);
  app.CanInteract=true;app.Settings.Current.movement=MoveStyle.Teleport;
  LearningXRInput.left=new(){tracked=true,source=XRInteractionSource.Controller};
  Tick(controls,1.65f);Physics.raycasts=0;
  LearningXRInput.left.axis=new(){y=1};Tick(controls,1.65f);
  LearningXRInput.left=new(){tracked=true,source=XRInteractionSource.BareHand};Tick(controls,1.65f);
  Check("controller-to-hand switch cancels pending teleport",Physics.raycasts==0);
  LearningXRInput.left=new(){tracked=true,source=XRInteractionSource.Controller,axis=new(){y=1}};Tick(controls,1.65f);
  LearningXRInput.left.axis=default;Tick(controls,1.65f);
  Check("controller returned with held stick does not teleport",Physics.raycasts==0);
  LearningXRInput.left.axis=new(){y=1};Tick(controls,1.65f);
  LearningXRInput.left.axis=default;Tick(controls,1.65f);
  Check("fresh controller push and release can request teleport",Physics.raycasts==1);
  LearningXRInput.left.axis=new(){y=1};Tick(controls,1.65f);
  Tick(controls,1.65f,false);LearningXRInput.left.axis=default;Tick(controls,1.65f);
  Check("head tracking recovery cannot finish old teleport",Physics.raycasts==1);
  LearningXRInput.left.axis=new(){y=1};Tick(controls,1.65f);
  app.CanInteract=false;Tick(controls,1.65f);app.CanInteract=true;LearningXRInput.left.axis=default;Tick(controls,1.65f);
  Check("unpausing cannot finish old teleport",Physics.raycasts==1);
  app.Settings.Current.movement=MoveStyle.Smooth;
  LearningXRInput.left.axis=default;Tick(controls,1.65f);
  var atRest=Camera.main.transform.parent.position;
  LearningXRInput.left.axis=new(){x=.1f,y=.1f};Tick(controls,1.65f);
  Check("smooth movement ignores joystick drift",(Camera.main.transform.parent.position-atRest).sqrMagnitude<.000001f);
  LearningXRInput.left.axis=new(){x=1,y=1};atRest=Camera.main.transform.parent.position;Tick(controls,1.65f);
  Check("diagonal walking cannot exceed configured speed",(Camera.main.transform.parent.position-atRest).magnitude<=app.Settings.Current.moveSpeed*Time.deltaTime+.00001f);
  LearningXRInput.left.axis=default;Tick(controls,1.65f);
  LearningXRInput.left.axis=new(){y=1};LearningXRInput.left.tracked=false;
  var before=Camera.main.transform.parent.position;Tick(controls,1.65f);
  Check("lost controller tracking blocks stale movement axes",(Camera.main.transform.parent.position-before).sqrMagnitude<.000001f);
  LearningXRInput.left.tracked=true;Tick(controls,1.65f);
  Check("held stick on recovery stays still until neutral",(Camera.main.transform.parent.position-before).sqrMagnitude<.000001f);
  LearningXRInput.left.axis=default;Tick(controls,1.65f);LearningXRInput.left.axis=new(){y=1};Tick(controls,1.65f);
  Check("movement returns after neutral then new stick input",(Camera.main.transform.parent.position-before).sqrMagnitude>.000001f);
  app.CanInteract=false;app.Focused=false;LearningXRInput.left.menu=true;Tick(controls,1.65f);
  Check("lost app focus blocks controller menu",app.UI.menuCount==0);
  app.Focused=true;Tick(controls,1.65f);
  Check("held menu on focus recovery does not replay",app.UI.menuCount==0);
  LearningXRInput.left.menu=false;Tick(controls,1.65f);LearningXRInput.left.menu=true;Tick(controls,1.65f);
  Check("fresh menu press after focus recovery works",app.UI.menuCount==1);
  Destroy(controls);
  Check("joystick walking is the default",new SettingsProfile().movement==MoveStyle.Smooth);
  var legacy=new SettingsProfile{schemaVersion=1,movement=MoveStyle.Teleport,heightBoost=.3f};legacy.Validate();
  Check("existing profiles gain joystick walking without losing height or cloud schema",legacy.schemaVersion==1&&legacy.movementRevision==1&&legacy.movement==MoveStyle.Smooth&&legacy.heightBoost==.3f);
  legacy.movement=MoveStyle.Teleport;legacy.Validate();
  Check("teleport remains an explicit saved comfort option",legacy.movement==MoveStyle.Teleport);
  Console.WriteLine($"PASS {checks} camera height and locomotion checks (actual LearningControls.cs; engine/XR doubles, not a physical headset).");return 0;
 }
}
