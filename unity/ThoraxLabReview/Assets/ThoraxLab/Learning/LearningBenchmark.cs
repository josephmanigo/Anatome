using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class LearningBenchmark : MonoBehaviour
    {
        IEnumerator Start()
        {
            var app=LabRuntime.Instance;var profile=app.Settings.Draft();profile.width=1280;profile.height=720;profile.frameCap=120;profile.SetPreset(GraphicsPreset.Medium);profile.narrationVolume=0;app.Settings.Apply(profile);
            yield return new WaitForSecondsRealtime(15);float finish=Time.realtimeSinceStartup+905;int unit=0;
            var scenarios=app.Catalog.units.SelectMany(u=>u.variants).Where(v=>v.difficulty==Difficulty.Easy).ToArray();
            while(Time.realtimeSinceStartup<finish)
            {
                app.SendMessage("OnApplicationFocus",true);app.StartActivity(scenarios[unit++%scenarios.Length],SessionPhase.Practice);
                float change=Time.realtimeSinceStartup+30;
                while(Time.realtimeSinceStartup<change&&Time.realtimeSinceStartup<finish)
                {
                    foreach(var structure in app.Structures.Values.Where(s=>s.gameObject.activeInHierarchy).Take(6)){app.Session.Select(structure);yield return new WaitForSecondsRealtime(2);}
                    if(app.Engine!=null&&app.Engine.Attempt.mode==LearningMode.AssembleAnatomy){foreach(var structure in app.Structures.Values.Where(s=>s.gameObject.activeInHierarchy).Take(3)){structure.RestorePose();structure.TrySnap();}}
                    if(app.CanCut&&app.Session.selected&&app.Session.selected.record.cutting_ready)
                    {
                        var s=app.Session.selected;var b=s.Renderer.bounds;var stroke=new System.Collections.Generic.List<float>();float extent=b.size.magnitude;
                        app.Session.saw=s.record.tissue=="bone";NativeCutting.AddPose(stroke,s.transform,b.center-Vector3.up*extent,Quaternion.identity,.0007f,extent*.15f,extent);NativeCutting.AddPose(stroke,s.transform,b.center+Vector3.up*extent,Quaternion.identity,.0007f,extent*.15f,extent);app.Session.Cut(stroke);while(app.Session.busy)yield return null;app.Session.Undo();
                    }
                    yield return new WaitForSecondsRealtime(1);
                }
            }
            var result=GetComponent<LearningPerformanceMonitor>().Capture();string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--qa-output");string output=index>=0?args[index+1]:Application.persistentDataPath;Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"windows-15-minute-benchmark.json"),JsonUtility.ToJson(result,true));Application.Quit();
        }
    }
}
