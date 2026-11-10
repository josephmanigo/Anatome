using UnityEditor;

namespace ThoraxLab.Editor
{
    [InitializeOnLoad]
    public static class LearningEditorPlayMode
    {
        const string Pending = "ThoraxLab.EditorPlay.Pending";
        static bool queued;

        static LearningEditorPlayMode()
        {
            if (SessionState.GetBool(Pending, false)) QueueAfterStartup();
        }

        public static void Request()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Pending, true);
            QueueAfterStartup();
        }

        public static void Cancel() => SessionState.SetBool(Pending, false);

        static void QueueAfterStartup()
        {
            if (queued) return;
            queued = true;
            // Unity 6000.3 creates its default Search database in delayCall.
            // A second dispatch lets the whole startup callback batch finish
            // in Edit mode, regardless of callback subscription order.
            EditorApplication.delayCall += () => EditorApplication.delayCall += EnterWhenReady;
        }

        static void EnterWhenReady()
        {
            if (!SessionState.GetBool(Pending, false)) { queued = false; return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnterWhenReady;
                return;
            }
            queued = false;
            bool requested = SessionState.GetBool(Pending, false);
            SessionState.SetBool(Pending, false);
            if (requested && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.EnterPlaymode();
        }
    }
}
