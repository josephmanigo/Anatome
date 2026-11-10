using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class LessonNarrator : MonoBehaviour
    {
        AudioSource source;
        string clipId,transcript;
        bool suspendedByFocus;
        string[] captions=System.Array.Empty<string>();
        public string Caption { get; private set; }
        public bool IsPlaying => source && source.isPlaying;
        public bool Paused { get; private set; }
        void Awake() { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; }
        public void Play(string id, string caption)
        {
            clipId = id; transcript = caption;var chunks=new System.Collections.Generic.List<string>();
            foreach(var sentence in System.Text.RegularExpressions.Regex.Split(caption??"",@"(?<=[.!?])\s+")){string part="";foreach(var word in sentence.Split(' ')){if(part.Length+word.Length>110&&part.Length>0){chunks.Add(part);part="";}part+=(part.Length>0?" ":"")+word;}if(part.Length>0)chunks.Add(part);}
            captions=chunks.ToArray();Caption=captions.Length>0?captions[0]:""; source.Stop(); source.clip = Resources.Load<AudioClip>("Learning/Voice/" + id); Paused = false;suspendedByFocus=false;
            // Apply saved audio controls before Play, including on a newly created guide source.
            var settings=LabRuntime.Instance?.Settings?.Current;
            if(settings!=null){source.volume=settings.narrationVolume;source.pitch=settings.narrationSpeed;}
            if (source.clip) source.Play();
        }
        public void TogglePause() { Paused = !Paused; if (Paused) source.Pause(); else if(LabRuntime.Instance?.Focused!=false)source.UnPause();else suspendedByFocus=true; }
        public void Replay() { if (!string.IsNullOrEmpty(clipId)) Play(clipId, transcript); }
        public void Stop() { source.Stop(); Caption = ""; Paused = false;suspendedByFocus=false; }
        void Update()
        {
            var settings = LabRuntime.Instance?.Settings?.Current; if (settings == null) return;
            source.volume = settings.narrationVolume; source.pitch = settings.narrationSpeed;
            if(!LabRuntime.Instance.Focused){if(source.isPlaying){source.Pause();suspendedByFocus=true;}return;}
            if(suspendedByFocus){suspendedByFocus=false;if(!Paused)source.UnPause();}
            if(source.clip&&source.isPlaying&&captions.Length>0){float position=source.time/source.clip.length;int length=0;int target=Mathf.FloorToInt(position*(transcript?.Length??0));foreach(string sentence in captions){length+=sentence.Length+1;if(length>=target){Caption=sentence;break;}}}
        }
    }
}
