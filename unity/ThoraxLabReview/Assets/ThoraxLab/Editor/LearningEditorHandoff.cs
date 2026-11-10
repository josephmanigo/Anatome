using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThoraxLab.Editor
{
    // The review Editor exits only when the local build workflow explicitly requests it.
    [InitializeOnLoad]
    public static class LearningEditorHandoff
    {
        static readonly string Request=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/vr-craft-editor-stop.request"));
        static LearningEditorHandoff()
        {
            if(!Application.isBatchMode&&File.Exists(Request))EditorApplication.delayCall+=StopForBuild;
        }
        static void StopForBuild()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying=false;
                EditorApplication.delayCall+=StopForBuild;return;
            }
            if(EditorApplication.isCompiling){EditorApplication.delayCall+=StopForBuild;return;}
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);if(!scene.isDirty)continue;
                string folder="Assets/ThoraxLab/Scenes/ReviewRecovery";Directory.CreateDirectory(folder);
                string path=folder+"/"+scene.name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
                if(!EditorSceneManager.SaveScene(scene,path,true))throw new IOException("Could not preserve dirty review scene: "+scene.name);
                Debug.Log("Preserved unsaved scene as a separate recovery copy: "+path);
            }
            File.Delete(Request);EditorApplication.Exit(0);
        }
    }
}
