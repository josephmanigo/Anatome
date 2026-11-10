using System.Collections;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class LessonDemonstrator : MonoBehaviour
    {
        GameObject ghost;Material material;Coroutine sequence;
        public bool Playing {get;private set;}
        public void Play()
        {
            Stop();var app=LabRuntime.Instance;
            if(app?.Engine==null||app.Engine.Attempt.phase==SessionPhase.Assessment)return;
            sequence=StartCoroutine(Demonstrate(app));
        }
        IEnumerator Demonstrate(LabRuntime app)
        {
            var objective=app.Engine.Current;if(objective==null)yield break;
            Playing=true;
            if(app.Structures.TryGetValue(objective.targetId,out var anatomy))
            {
                anatomy.Highlight(true);
                if(objective.action==ActionKind.Place||objective.action==ActionKind.Expose)
                {
                    ghost=new GameObject("Teaching demonstration · no collision");ghost.AddComponent<MeshFilter>().sharedMesh=anatomy.Filter.sharedMesh;
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.2f,.78f,.70f);ghost.AddComponent<MeshRenderer>().sharedMaterial=material;
                    ghost.transform.SetParent(anatomy.transform.parent,false);ghost.transform.localScale=anatomy.transform.localScale;
                    var start=anatomy.transform.position;var end=objective.action==ActionKind.Place?anatomy.transform.parent.TransformPoint(anatomy.RestPosition):start+Vector3.up*.18f;
                    var initial=anatomy.transform.rotation;var final=objective.action==ActionKind.Place?anatomy.transform.parent.rotation*anatomy.RestRotation:initial;
                    float time=0;while(time<3&&app.Engine!=null&&app.Engine.Current==objective)
                    {if(app.CanInteract&&!app.Narrator.Paused)time+=Time.deltaTime;float t=app.Settings.Current.reducedMotion?1:Mathf.SmoothStep(0,1,time/3);ghost.transform.SetPositionAndRotation(Vector3.Lerp(start,end,t),Quaternion.Slerp(initial,final,t));yield return null;}
                }
                else{app.Procedure.ShowHint();yield return new WaitForSeconds(2);}
                if(anatomy)anatomy.Highlight(false);
            }
            else app.Procedure.ShowHint();
            app.Message("Demonstration paused. Perform the objective on the specimen to continue.");Stop();
        }
        public void Stop(){if(sequence!=null)StopCoroutine(sequence);sequence=null;if(ghost)Destroy(ghost);if(material)Destroy(material);Playing=false;}
        void OnDestroy()=>Stop();
    }
}
