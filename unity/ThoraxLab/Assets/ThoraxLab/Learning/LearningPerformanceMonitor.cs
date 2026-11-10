using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class LearningPerformanceMonitor : MonoBehaviour
    {
        public static string Display {get;private set;}="Measuring FPS";
        readonly List<float> samples=new List<float>(100000);
        float interval,total,lowSeconds;int count;
        bool suggested,paused,skipNextFrame;
        void Update()
        {
            if(paused)return;if(skipNextFrame){skipNextFrame=false;return;}
            float dt=Time.unscaledDeltaTime;if(dt<=0)return;total+=dt;
            if(total>10&&samples.Count<150000)samples.Add(dt);
            interval+=dt;count++;if(interval<1)return;
            float fps=count/interval;Display=Mathf.RoundToInt(fps)+" FPS";interval=count=0;
            var app=LabRuntime.Instance;if(app?.Settings==null)return;
            float target=app.Settings.Platform=="Quest"?(QuestRefreshFeature.Active?QuestRefreshFeature.Active.RequestedRate:0):app.Settings.Current.frameCap;
            lowSeconds=target>0&&fps<target*.9f?lowSeconds+1:Mathf.Max(0,lowSeconds-1);
            if(lowSeconds>=20&&!suggested){suggested=true;app.Message("Performance is below the "+target+" FPS target. Try a lower graphics preset in Settings. Your settings have not been changed.");}
        }
        [Serializable] public class Measurement {public string utc,appVersion,package,graphicsApi,device,gpu,platform,preset;public int frames;public float requestedRefresh,seconds,averageFps,onePercentLow;public bool fifteenMinutes;}
        public Measurement Capture()
        {
            var ordered=samples.OrderByDescending(v=>v).ToArray();int n=Mathf.Max(1,Mathf.CeilToInt(ordered.Length*.01f));float duration=samples.Sum();
            return new Measurement{utc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,package=Application.identifier,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),requestedRefresh=QuestRefreshFeature.Active?QuestRefreshFeature.Active.RequestedRate:0,device=SystemInfo.deviceModel,gpu=SystemInfo.graphicsDeviceName,platform=Application.platform.ToString(),preset=LabRuntime.Instance?.Settings.Current.preset.ToString(),frames=samples.Count,seconds=duration,averageFps=duration>0?samples.Count/duration:0,onePercentLow=ordered.Length>0?1/ordered.Take(n).Average():0,fifteenMinutes=duration>=900};
        }
        void OnApplicationPause(bool value){paused=value;skipNextFrame=true;interval=count=0;if(value)SaveMeasurement();}
        void OnApplicationQuit()=>SaveMeasurement();
        void SaveMeasurement(){if(samples.Count==0)return;try{File.WriteAllText(Path.Combine(Application.persistentDataPath,"learning-performance.json"),JsonUtility.ToJson(Capture(),true));}catch(IOException ex){Debug.LogWarning("Performance report could not be saved: "+ex.Message);}catch(UnauthorizedAccessException ex){Debug.LogWarning("Performance report could not be saved: "+ex.Message);}}
    }
}
