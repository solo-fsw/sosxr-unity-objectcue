using UnityEditor;
using UnityEngine;


namespace SOSXR.ObjectCue
{
    [CustomEditor(typeof(ObjectCue))]
    [CanEditMultipleObjects]
    public class ObjectCueEditor : Editor
    {
        private ObjectCue _objectCue;


        private void OnEnable()
        {
            _objectCue = (ObjectCue) target;
        }


        public override void OnInspectorGUI()
        {
            if (GUILayout.Button(nameof(ObjectCue.StartCue)))
            {
                _objectCue.StartCue();
            }

            if (GUILayout.Button(nameof(ObjectCue.StopCue)))
            {
                _objectCue.StopCue();
            }

            if (GUILayout.Button(nameof(ObjectCue.ToggleCue)))
            {
                _objectCue.ToggleCue();
            }

            GUILayout.Space(10);
            
            base.OnInspectorGUI();
        }
    }
}