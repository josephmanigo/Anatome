using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Learning
{
    // Numbers are attached to the selected surface, not to an arbitrary mesh pivot.
    // Only accepted unique objectives create a marker; wrong/duplicate taps cannot
    // inflate the set count. Markers are reconstructed from saved action evidence.
    public sealed class EnumerationMarkers : MonoBehaviour
    {
        readonly Dictionary<string,GameObject> markers=new Dictionary<string,GameObject>();
        LabRuntime app;
        public int Count => markers.Count;
        public void Initialize(LabRuntime runtime) { app=runtime; }
        public void Rebuild()
        {
            Clear();
            if(app?.Variant?.mode!=LearningMode.IdentifyEnumerate||app.Engine==null)return;
            foreach(var objective in app.Variant.objectives)
            {
                var result=app.Engine.Attempt.objectives.FirstOrDefault(o=>o.objectiveId==objective.id&&o.completed);
                var action=result==null?null:app.Engine.Attempt.actions.FirstOrDefault(a=>a.id==result.actionId);
                if(action!=null)Add(objective,action);
            }
        }
        public void Add(ObjectiveDefinition objective,InteractionAction action)
        {
            if(markers.ContainsKey(objective.id)||!app.Structures.TryGetValue(objective.targetId,out var structure))return;
            var marker=new GameObject("Enumeration marker "+(System.Array.IndexOf(app.Variant.objectives,objective)+1),typeof(RectTransform));
            marker.transform.SetParent(structure.transform,false);
            var local=action.localPosition;
            marker.transform.position=local!=null&&local.Length==3?structure.transform.TransformPoint(new Vector3(local[0],local[1],local[2])):structure.Renderer.bounds.center;
            var toward=Camera.main?(Camera.main.transform.position-marker.transform.position).normalized:Vector3.up;
            marker.transform.position+=toward*.008f;
            marker.transform.localScale=Vector3.one*.00035f/Mathf.Max(.0001f,structure.transform.lossyScale.x);
            var rect=(RectTransform)marker.transform;rect.sizeDelta=new Vector2(60,60);
            var canvas=marker.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;
            var background=marker.AddComponent<Image>();background.color=new Color(.025f,.065f,.075f);background.raycastTarget=false;
            var text=LabWidgets.Text(rect,(System.Array.IndexOf(app.Variant.objectives,objective)+1).ToString(),38,Color.white);
            LabWidgets.Fill(text.rectTransform);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            markers.Add(objective.id,marker);
        }
        void LateUpdate()
        {
            if(!Camera.main)return;
            foreach(var marker in markers.Values)if(marker&&marker.activeInHierarchy)marker.transform.rotation=Quaternion.LookRotation(marker.transform.position-Camera.main.transform.position,Vector3.up);
        }
        public void Clear(){foreach(var marker in markers.Values)if(marker)Destroy(marker);markers.Clear();}
        void OnDestroy()=>Clear();
    }
}
