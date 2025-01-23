using SOSXR.EditorTools;
using UnityEditor;
using UnityEngine;


namespace SOSXR.ObjectCue
{
    // [CustomEditor(typeof(ObjectCue))]
    [CanEditMultipleObjects]
    public class ObjectCueEditor : EditorGUIHelpers
    {
        private ObjectCue _target;


        private void OnEnable()
        {
            _target = (ObjectCue) target;
        }


        protected override void CustomInspectorContent()
        {
            DefaultVerticalBoxedLayout(DrawButtons);

            DefaultVerticalBoxedLayout(DrawAutoStartProperties);
            DefaultVerticalBoxedLayout(DrawLoopingProperties);
            DefaultVerticalBoxedLayout(DrawReturnProperties);

            DefaultVerticalBoxedLayout(DrawPositionProperties);
            DefaultVerticalBoxedLayout(DrawRotationProperties);
            DefaultVerticalBoxedLayout(DrawScaleProperties);

            DefaultVerticalBoxedLayout(DrawStartSoundProperties);
            DefaultVerticalBoxedLayout(DrawStopSoundProperties);

            DefaultVerticalBoxedLayout(DrawColorProperties);
            DefaultVerticalBoxedLayout(DrawEmissionProperties);

            DefaultVerticalBoxedLayout(DrawUnityEventProperties);

            CreateCustomInspectorToggleButtons();
        }


        private void DrawButtons()
        {
            GUILayout.BeginHorizontal();

            if (CreateButton("TEST START CUE", 10))
            {
                _target.StartCue();
            }

            if (CreateButton("TEST STOP CUE", 10))
            {
                _target.StopCue();
            }

            GUILayout.EndHorizontal();
        }


        private void DrawAutoStartProperties()
        {
            if (!CreateHeaderToggle(nameof(_target.AutoStart), "AUTOSTART"))
            {
                return;
            }

            CreateFloatSliderProperty(nameof(_target.StartDelay));
        }


        private void DrawLoopingProperties()
        {
            const string toolTip1 = "Loop duration in seconds (this is a full loop, so including the return to start values)";
            CreateFloatSliderProperty(nameof(_target.CueLoopDuration), toolTip1, 0.1f);

            const string toolTip2 = "The amount of times this loop should repeat. -1 is infinite, 0 is do not loop";
            CreateIntSliderProperty(nameof(_target.NumberOfLoops), toolTip2, -1);

            const string toolTip3 = "In seconds. In case ReturnSequence is active when CueSequence is called again, this is the duration to which the object will transition back to original values, prior to starting new CueSequence";
            CreateFloatSliderProperty(nameof(_target.GracefulTransitionDuration), toolTip3, 0.1f, 3f);
        }


        private void DrawReturnProperties()
        {
            CreateEnumField(nameof(_target.ReturnDurationType), typeof(ReturnDurationType), "Return Duration Type");

            const string toolTip5 = "Return duration in seconds: Once all loops are done / looping in stopped, how long does it take to transition back to original / starting values?";
            CreateFloatSliderProperty(nameof(_target.ReturnDuration), toolTip5, 0.1f);

            const string toolTip6 = "A returnCurve starting low (0,0) and ending high (1,0.99f) seems to work well. When final value is set to 1 instead of 0.99f, flickering (in Emission) occurs.";
            CreateAnimationCurveField(nameof(_target.ReturnCurve), toolTip6, endValue: 0.99f);
        }


        private void DrawPositionProperties()
        {
            CreateVector3Field(nameof(_target.AddedLocalPosition));
            const string toolTip7 = "A returnCurve starting low (0,0) and ending high (1, 1f) seems to work well.";
            CreateAnimationCurveField(nameof(_target.PositionCurve), toolTip7);
        }


        private void DrawRotationProperties()
        {
            CreateVector3Field(nameof(_target.AddedLocalRotation));

            const string toolTip8 = "A returnCurve starting low (0,0) and ending high (1, 1f) seems to work well.";
            CreateAnimationCurveField(nameof(_target.RotationCurve), toolTip8);
        }


        private void DrawScaleProperties()
        {
            CreateVector3Field(nameof(_target.AddedLocalScale));

            const string toolTip9 = "A returnCurve starting low (0,0) and ending high (1, 1f) seems to work well.";
            CreateAnimationCurveField(nameof(_target.ScaleCurve), toolTip9);
        }


        private void DrawStartSoundProperties()
        {
            // const string toolTip10 = "This sound will be played at each 'halfway-point' of the loop: at start, once reached max values, upon returning to original / starting values. \n" +
            //                          "It will not be played at final rest (upon reaching origianl starting values, once loops are done / stopped).";


            CreatePropertyField(nameof(_target.CueClip));
            CreateToggleProperty(nameof(_target.PlayHalfWay));
        }


        private void DrawStopSoundProperties()
        {
            // const string toolTip11 = "Sound that will be played at very last reaching of the original values, at the very end of all the loops.";

            CreatePropertyField(nameof(_target.StopClip));
        }


        private void DrawColorProperties()
        {
            if (!CreateHeaderToggle(nameof(_target.UseColor), "COLOR SETTINGS"))
            {
                return;
            }

            GUILayout.Space(DefaultSmallSpace);
            GUILayout.BeginVertical(EditorStyles.helpBox);


            const string toolTip13 = "Make sure you manually add all required Renderers, if they're not on this GameObject or one of it's direct children \n" +
                                     "Will try to find on current GameObject, or direct child of GameObject, if null.";

            CreatePropertyField(nameof(_target.Renderers), toolTip13);


            CreateAnimationCurveField(nameof(_target.ColorCurve));

            GUILayout.EndVertical();

            CreateColorField(nameof(_target.DesiredColor), false);
        }


        private void DrawEmissionProperties()
        {
            GUILayout.Space(DefaultSmallSpace);

            if (!_target.UseColor)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);


                const string toolTip13 = "Make sure you manually add all required Renderers, if they're not on this GameObject or one of it's direct children \n" +
                                         "Will try to find on current GameObject, or direct child of GameObject, if null. \n" +
                                         "Make sure that the Global Illumination on Renderer is set to Realtime or None, not to Baked. \n" +
                                         "Also make sure 'Specular highlights' is enabled!";

                CreatePropertyField(nameof(_target.Renderers), toolTip13);


                GUILayout.EndVertical();
            }

            const string toolTip14 = "Make sure you manually toggle 'Emission' on all Materials on all MeshRenderers used in the loop! \n" +
                                     "Make sure that the Global Illumination on Renderer is set to Realtime or None, not to Baked. \n" +
                                     "Also make sure 'Specular highlights' is enabled!";

            //const string toolTip16 = "White works well. No need to set intensity.";
            //CreateColorField(nameof(_target.DesiredEmissionColor), true, toolTip16);

           // CreateAnimationCurveField(nameof(_target.EmissionCurve));
        }


        private void DrawUnityEventProperties()
        {
            CreatePropertyField(nameof(_target.EventOnStart));
            CreatePropertyField(nameof(_target.EventOnStop));
        }
    }
}