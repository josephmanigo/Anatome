using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class LabAudio : MonoBehaviour
    {
        AudioSource ambience,effects;AudioClip hum,accept,retry;
        void Awake()
        {
            ambience=gameObject.AddComponent<AudioSource>();effects=gameObject.AddComponent<AudioSource>();ambience.playOnAwake=effects.playOnAwake=false;ambience.spatialBlend=effects.spatialBlend=0;
            hum=Tone("Lab ventilation",120,2,.08f,true);accept=Tone("Objective feedback",720,.085f,.14f,false);retry=Tone("Revision feedback",280,.14f,.12f,false);ambience.clip=hum;ambience.loop=true;ambience.Play();
        }
        static AudioClip Tone(string name,float frequency,float seconds,float gain,bool loop)
        {
            const int rate=24000;var data=new float[Mathf.RoundToInt(seconds*rate)];for(int i=0;i<data.Length;i++){float t=(float)i/rate;float envelope=loop?1:Mathf.Sin(Mathf.PI*i/data.Length);data[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*envelope*gain;}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Feedback(bool accepted){effects.PlayOneShot(accepted?accept:retry);}
        void Update(){var p=LabRuntime.Instance?.Settings?.Current;if(p==null)return;ambience.volume=p.ambienceVolume;effects.volume=p.effectsVolume;}
        void OnDestroy(){foreach(var clip in new[]{hum,accept,retry})if(clip)Destroy(clip);}
    }
}
