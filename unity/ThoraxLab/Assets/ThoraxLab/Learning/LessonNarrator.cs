using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class LessonNarrator : MonoBehaviour
    {
        AudioSource source;
        string clipId,transcript;
        bool waitingForAudio, started, suspended;
        System.Action onFinished;
        string[] captions=System.Array.Empty<string>();
        public string Caption { get; private set; }
        public bool IsPlaying => source && source.isPlaying;
        public bool HasPlayback => waitingForAudio || started;
        public bool Paused { get; private set; }
        void Awake() { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; }
        public void Play(string id, string caption, System.Action then = null)
        {
            Stop();
            clipId = id; transcript = caption;var chunks=new System.Collections.Generic.List<string>();
            foreach(var sentence in System.Text.RegularExpressions.Regex.Split(caption??"",@"(?<=[.!?])\s+")){string part="";foreach(var word in sentence.Split(' ')){if(part.Length+word.Length>110&&part.Length>0){chunks.Add(part);part="";}part+=(part.Length>0?" ":"")+word;}if(part.Length>0)chunks.Add(part);}
            captions=chunks.ToArray();Caption=captions.Length>0?captions[0]:"";
            source.clip = Resources.Load<AudioClip>("Learning/Voice/" + id);
            onFinished = then;
            // Apply saved audio controls before Play, including on a newly created guide source.
            var settings=LabRuntime.Instance?.Settings?.Current;
            if(settings!=null){source.volume=settings.narrationVolume;source.pitch=settings.narrationSpeed;}
            if (!source.clip || (source.clip.loadState == AudioDataLoadState.Unloaded && !source.clip.LoadAudioData()))
            {
                Debug.LogWarning("Could not load narration: " + id);
                Complete(); return;
            }
            waitingForAudio = true;
            UpdatePlayback();
        }
        public void TogglePause()
        {
            if (!HasPlayback) return;
            Paused = !Paused;
            UpdatePlayback();
        }
        public void Replay() { if (!string.IsNullOrEmpty(clipId)) Play(clipId, transcript, onFinished); }
        public void Stop()
        {
            source.Stop(); source.clip = null; clipId = null; Caption = "";
            waitingForAudio = started = suspended = Paused = false; onFinished = null;
        }
        void Complete()
        {
            var next = onFinished;
            waitingForAudio = started = suspended = false; onFinished = null;
            next?.Invoke();
        }
        void UpdatePlayback()
        {
            if (!HasPlayback) return;
            if (Paused || LabRuntime.Instance?.Focused == false)
            {
                if (started && !suspended) { source.Pause(); suspended = true; }
                return;
            }
            if (waitingForAudio)
            {
                if (source.clip.loadState == AudioDataLoadState.Failed)
                {
                    Debug.LogWarning("Could not decode narration: " + clipId); Complete(); return;
                }
                if (source.clip.loadState != AudioDataLoadState.Loaded) return;
                source.Play(); waitingForAudio = false; started = true;
            }
            else if (suspended) { source.UnPause(); suspended = false; }
            else if (!source.isPlaying) Complete();
        }
        void Update()
        {
            var settings = LabRuntime.Instance?.Settings?.Current; if (settings == null) return;
            source.volume = settings.narrationVolume; source.pitch = settings.narrationSpeed;
            UpdatePlayback();
            if(source.clip&&source.isPlaying&&captions.Length>0){float position=source.time/source.clip.length;int length=0;int target=Mathf.FloorToInt(position*(transcript?.Length??0));foreach(string sentence in captions){length+=sentence.Length+1;if(length>=target){Caption=sentence;break;}}}
        }
    }
}
