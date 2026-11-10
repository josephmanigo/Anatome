using System;
using ThoraxLab.Learning;
int count=0;
void Check(string label,bool pass){if(!pass)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
var gestures=new HandGestureLatch();
gestures.Update(true,.1f,.1f);Check("open hand is neutral",!gestures.Select&&!gestures.Grab);
gestures.Update(true,.85f,.1f);Check("index pinch selects without grabbing",gestures.Select&&!gestures.Grab);
gestures.Update(true,.6f,.1f);Check("pinch hysteresis avoids chatter",gestures.Select);
gestures.Update(true,.4f,.1f);Check("pinch release clears selection",!gestures.Select);
gestures.Update(true,.1f,.85f);Check("three-finger grasp holds without activating tool",gestures.Grab&&!gestures.Select);
gestures.Update(true,.85f,.9f);Check("index can activate while remaining fingers hold",gestures.Select&&gestures.Grab);
gestures.Update(true,.1f,.6f);Check("grasp hysteresis holds through minor noise",gestures.Grab&&!gestures.Select);
gestures.Update(true,.1f,.4f);Check("opening releases the object",!gestures.Grab);
gestures.Update(true,1,1);gestures.Update(false,1,1);Check("tracking loss clears held gestures",!gestures.Select&&!gestures.Grab);
foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){gestures.Update(true,1,1);gestures.Update(true,invalid,1);Check("invalid pinch cancels both gestures "+invalid,!gestures.Select&&!gestures.Grab);gestures.Update(true,1,invalid);Check("invalid grasp cancels both gestures "+invalid,!gestures.Select&&!gestures.Grab);}
var gate=new XRInteractionGate();
void Input(string label,XRInteractionSource source,bool tracked,bool pinch,bool grasp,bool expectedPinch,bool expectedGrasp){gate.Filter(source,tracked,ref pinch,ref grasp);Check(label,pinch==expectedPinch&&grasp==expectedGrasp);}
Input("held controller at launch cannot select",XRInteractionSource.Controller,true,true,true,false,false);
Input("controller neutral arms inputs",XRInteractionSource.Controller,true,false,false,false,false);
Input("new controller press works",XRInteractionSource.Controller,true,true,true,true,true);
Input("switch to closed bare hand cancels old action",XRInteractionSource.BareHand,true,true,true,false,false);
Input("held hand after switch stays blocked",XRInteractionSource.BareHand,true,true,true,false,false);
Input("open hand arms gestures",XRInteractionSource.BareHand,true,false,false,false,false);
Input("new pinch selects",XRInteractionSource.BareHand,true,true,false,true,false);
Input("new grasp is independent",XRInteractionSource.BareHand,true,false,true,false,true);
Input("tracking/system gesture cancellation releases",XRInteractionSource.BareHand,false,true,true,false,false);
Input("closed hand at recovery stays blocked",XRInteractionSource.BareHand,true,true,true,false,false);
Input("release only trigger re-arms only trigger",XRInteractionSource.BareHand,true,false,true,false,false);
Input("trigger can work while stale grip remains blocked",XRInteractionSource.BareHand,true,true,true,true,false);
Input("switch back to held controller does not grab",XRInteractionSource.Controller,true,true,true,false,false);
Input("controller release arms again",XRInteractionSource.Controller,true,false,false,false,false);
Input("controller works again",XRInteractionSource.Controller,true,true,true,true,true);
Input("no source releases everything",XRInteractionSource.None,false,true,true,false,false);
Console.WriteLine($"PASS {count} gesture and input-switch checks.");
