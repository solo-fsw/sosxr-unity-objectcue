 /*using System;
using SOSXR.Tweening;
using UnityEngine;
using UnityEngine.Events;


namespace SOSXR.ObjectCue
{
    [Serializable]
    public class ObjectCueOld : MonoBehaviour
    {
        [Header("AUTOSTART")]
        [Space(10)]
        public bool AutoStart;
        public float StartDelay = 5f;


        [Header("LOOPING AND RETURN")]
        [Space(10)]
        public bool ChangeLooping;
        [Tooltip("Loop duration in seconds (this is a full loop, so including the return to start values)")]
        [Range(0.1f, 10f)] public float CueLoopDuration = 2.5f;

        [Tooltip("The amount of times this loop should repeat. -1 is infinite, 0 is do not loop")]
        [Range(-1, 10)] public int NumberOfLoops = -1;

        [Tooltip("In seconds. In case ReturnSequence is active when CueSequence is called again, this is the duration to which the object will transition back to original values, prior to starting new CueSequence")]
        [Range(0.1f, 3f)] public float GracefulTransitionDuration = 0.5f;

        [Tooltip("If false, the remaining time in the running CueSequence will be used as the duration for which the ReturnSequence runs for.")]
        public bool SetReturnDuration;

        [Tooltip("Return duration in seconds: Once all loops are done / looping in stopped, how long does it take to transition back to original / starting values?")]
        [Range(0.1f, 10f)] public float ReturnDuration = 1f;

        [Tooltip("A returnCurve starting low (0,0) and ending high (1,0.99f) seems to work well. When final value is set to 1 instead of 0.99f, flickering (in Emission) occurs.")]
        public AnimationCurve ReturnCurve = new(new Keyframe(0, 0), new Keyframe(1, 0.99f));


        [Header("LOCAL POSITION SETTINGS")]
        [Space(10)]
        public bool UsePosition;
        public Vector3 AddedLocalPosition = new(0f, 0f, 0f);
        public AnimationCurve PositionCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));


        [Header("LOCAL ROTATION SETTINGS")]
        [Space(10)]
        public bool UseRotation;
        public Vector3 AddedLocalRotation = new(0f, 0f, 0f);
        public AnimationCurve RotationCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));


        [Header("SCALE SETTINGS")]
        [Space(10)]
        public bool UseScale;
        public Vector3 AddedScale = new(0f, 0f, 0f);
        public AnimationCurve ScaleCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));


        [Header("AUDIO SETTINGS")]
        [Space(10)]
        public bool UseCueSound;
        public AudioClip CueClip;
        [Tooltip("This sound will be played at each 'halfway-point' of the loop: at start, once reached max values, upon returning to original / starting values. It will not be played at final rest (upon reaching origianl starting values, once loops are done / stopped).")]
        public bool PlayHalfWay;

        public bool UseStopSound;
        [Tooltip("Sound that will be played at very last reaching of the original values, at the very end of all the loops.")]
        public AudioClip StopClip;


        [Header("COLOR SETTINGS")]
        [Space(10)]
        public bool UseColor;
        [ColorUsage(true)] public Color DesiredColor = Color.white;
        public AnimationCurve ColorCurve = new(new Keyframe(0, 0.05f), new Keyframe(1, 1f));

        [Header("EMISSION SETTINGS")]
        [Space(10)]
        [Tooltip("Make sure you manually toggle 'Emission' on all Materials on all MeshRenderers used in the loop! Also make sure 'SpecularHighlights' is enabled!")]
        public bool UseEmission;
        public bool SetBaseEmissionColor;

        [Tooltip("If you want to preset the original / starting emission color: black is shown to work well, because it has 'no emission'. Keep 'intensity' at 0.")]
        [ColorUsage(true, true)] public Color BaseEmissionColor = Color.black;

        [Tooltip("White works well. No need to set intensity.")]
        [ColorUsage(true, true)] public Color DesiredEmissionColor = Color.white;

        [Tooltip("Emission curve should probably never hit 0, because then flickering occurs.")]
        public AnimationCurve EmissionCurve = new(new Keyframe(0, 0.05f), new Keyframe(1, 0.35f));
        public bool UseRenderer = true;
        [Tooltip("REQUIRED IF USING EMISSION! Will try to find on current GameObject, or child of GameObject, if null. " + "\n Make sure that the Global Illumination is set to Realtime or None, not to Baked")]
        public Renderer[] Renderers = { };
        public Material[] Materials;

        [Header("EVENT SETTINGS")]
        [Space(10)]
        [Tooltip("For if this Cue needs to communicate with some kind of event, e.g. for UXF to log start and stop times.")]
        public bool FireCueStartEvent;
        public bool FireEventOnlyOnce;
        public UnityEvent StartCueEvent;
        public UnityEvent StopCueEvent;


        [Space(10)]
        [Header("UNITY EVENTS")]
        public UnityEvent EventOnStart;
        public UnityEvent EventOnStop;

        [Header("PREVENT STARTING CUE")]
        public bool PreventStartingCue;

        private AudioSource _audioSource;

        private int _baseColorID = Shader.PropertyToID("_BaseColor");

        //private Sequence _cueSequence;
        // [SerializeField]    private List<TweenInstance> _cueSequence;

        private int _emissionColorID = Shader.PropertyToID("_EmissionColor");

        private bool _eventFired;
        private Material[] _materials;
        private Color[] _originalBaseColors;
        private Color[] _originalEmissionColors;

        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _originalScale;

        private float _remainingDurationInCueSequence;
        //private Sequence _rescueSequence;
        //private Sequence _returnSequence;

        public bool CueIsActive { get; set; }

        public float HalfLoopDuration => CueLoopDuration / 2; // DoTween counts one way (from starting values to max values) as 1 loop, whereas I think is more logical to count a loop from 0 to full and back to 0

        public int TweenNumberOfLoops => NumberOfLoops >= 1 ? NumberOfLoops * 2 : NumberOfLoops; // DoTween counts one way (from 0 to full) as a loop, whereas I think is more logical to count a loop from 0 to full and back to 0


        public float ReturnSequenceDuration
        {
            get => SetReturnDuration ? ReturnDuration : _remainingDurationInCueSequence;

            set => _remainingDurationInCueSequence = value;
        }


        public void Awake()
        {
            FindMeshRenderersOnComponentOrChildren();
        }


        public void FindMeshRenderersOnComponentOrChildren()
        {
            Renderers ??= GetComponentsInChildren<Renderer>();
        }


        public void Start()
        {
            if (UseEmission && SetBaseEmissionColor)
            {
                BaseEmissionColor *= 0;
            }

            PrepareEmissionColors();

            StoreOriginalTransform();
        }


        private void OnEnable()
        {
            if (AutoStart)
            {
                Invoke(nameof(StartCue), StartDelay);
            }
        }


        public void PrepareEmissionColors()
        {
            if (UseEmission == false && UseColor == false)
            {
                return;
            }

            if (Renderers != null && UseRenderer)
            {
                _materials = new Material[Renderers.Length];

                for (var i = 0; i < Renderers.Length; i++)
                {
                    _materials[i] = Renderers[i].material;
                }
            }
            else if (Materials != null && !UseRenderer)
            {
                _materials = new Material[Materials.Length];
                _materials = Materials;
            }

            _originalEmissionColors = new Color[_materials.Length];
            _originalBaseColors = new Color[_materials.Length];

            for (var i = 0; i < _materials.Length; i++)
            {
                _materials[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                _materials[i].EnableKeyword("_EMISSION");
                _materials[i].DisableKeyword("_SPECULARHIGHLIGHTS_OFF");

                if (SetBaseEmissionColor)
                {
                    _materials[i].SetColor(_emissionColorID, BaseEmissionColor);
                }

                _originalEmissionColors[i] = _materials[i].GetColor(_emissionColorID);
                _originalBaseColors[i] = _materials[i].GetColor(_baseColorID);
            }
        }


        public void StoreOriginalTransform()
        {
            var trans = transform;
            _originalScale = trans.localScale;
            _originalPosition = trans.localPosition;
            _originalRotation = trans.localRotation;
        }


        public void ToggleCue(bool start)
        {
            if (start)
            {
                StartCue();
            }
            else
            {
                StopCue();
            }
        }


        [ContextMenu(nameof(StartCue))]
        public void StartCue()
        {
            if (Application.isPlaying == false)
            {
                return;
            }

            //var positionTween = new PositionTween
            //{
            //    to = AddedLocalPosition,
            //    duration = HalfLoopDuration
            //};

            transform.TweenLocalPosition(AddedLocalPosition, HalfLoopDuration);

            //var rotationTween = new EulerAnglesTween
            //{
            //    to =  AddedLocalRotation,
            //    duration = HalfLoopDuration
            //};

            transform.TweenLocalRotation(AddedLocalRotation, HalfLoopDuration);
            // _cueSequence.Add(gameObject.AddTween(positionTween));
            // _cueSequence.Add(gameObject.AddTween(rotationTween));

            //if  (_cueSequence != null && !_cueSequence.isPaused) // Don't start CueSequence if already playing
            //{
            //    Debug.Log("Cue sequence is active, cannot restart cue sequence");
            //    return;
            //}

            EventOnStart?.Invoke();

            //if (_returnSequence.IsActive()) // Makes sure this CueSequence can always run, even when ReturnSequence is currently active.
            //{
            //    _returnSequence.Kill();//
            //    CreateGracefulTransitionBetweenReturnSequenceAndCueSequence();
            //}
            //else
            //{
            if (UseCueSound && PlayHalfWay == false)
            {
                PlayCueSound(); // To ensure sound is played at start of loop as well as end.
            }

            FireCueEvent();

            CreateCueLoop();
            //}
        }


        public void FireCueEvent()
        {
            if (FireCueStartEvent == false)
            {
                return;
            }

            if (FireEventOnlyOnce && _eventFired)
            {
                return;
            }

            if (StartCueEvent == null)
            {
                return;
            }

            StartCueEvent?.Invoke();

            _eventFired = true;
        }


        public void CreateGracefulTransitionBetweenReturnSequenceAndCueSequence()
        {
            // _rescueSequence = DOTween.Sequence();

            if (Renderers != null && _originalEmissionColors != null && _originalEmissionColors.Length > 0)
            {
                for (var i = 0; i < _originalEmissionColors.Length; i++)
                {
                    //_rescueSequence.Insert(0, _materials[i].DOColor(_originalEmissionColors[i], _emissionColorID, GracefulTransitionDuration).SetEase(ReturnCurve));
                    _materials[i].TweenEmission(_originalEmissionColors[i], GracefulTransitionDuration).WithEase(ReturnCurve);
                }
            }

            transform.TweenScale(_originalScale, GracefulTransitionDuration).WithEase(ReturnCurve);
            transform.TweenLocalPosition(_originalPosition, GracefulTransitionDuration).WithEase(ReturnCurve);
            transform.TweenLocalRotation(_originalRotation.eulerAngles, GracefulTransitionDuration).WithEase(ReturnCurve).OnComplete += CreateCueLoop;

            // _rescueSequence.Insert(0, transform.DOScale(_originalScale, GracefulTransitionDuration).SetEase(ReturnCurve));
            // _rescueSequence.Insert(0, transform.DOLocalMove(_originalPosition, GracefulTransitionDuration).SetEase(ReturnCurve));
            // _rescueSequence.Insert(0, transform.DOLocalRotate(_originalRotation.eulerAngles, GracefulTransitionDuration).SetEase(ReturnCurve));

            // _rescueSequence.onComplete += CreateCueLoop;
        }


        public void PlayCueSound()
        {
            PlayClipAtPoint(CueClip);
        }


        private void PlayClipAtPoint(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogError("We want to PlayClipAtPoint, but we haven't supplied a clip. That cannot be");

                return;
            }

            if (_audioSource == null && GetComponent<AudioSource>() == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.spatialize = true;
            _audioSource.spatialBlend = 1;
            _audioSource.minDistance = 0;
            _audioSource.maxDistance = 5;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;

            _audioSource.clip = clip;
            _audioSource.Play();
        }


        public void CreateCueLoop()
        {
            if (PreventStartingCue)
            {
                return;
            }

            _cueSequence = DOTween.Sequence();

            CueIsActive = true;

            if (UsePosition)
            {
                var pos = _originalPosition + AddedLocalPosition;
                _cueSequence.Insert(0, transform.DOLocalMove(pos, HalfLoopDuration).SetEase(PositionCurve));
            }

            if (UseRotation)
            {
                var rot = _originalRotation.eulerAngles + AddedLocalRotation;
                _cueSequence.Insert(0, transform.DOLocalRotate(rot, HalfLoopDuration).SetEase(RotationCurve));
            }

            if (UseScale)
            {
                var scale = _originalScale + AddedScale;
                _cueSequence.Insert(0, transform.DOScale(scale, HalfLoopDuration).SetEase(ScaleCurve));
            }

            if (UseCueSound && PlayHalfWay)
            {
                _cueSequence.InsertCallback(HalfLoopDuration / 2, PlayCueSound);
            }
            else if (UseCueSound && !PlayHalfWay)
            {
                _cueSequence.OnStepComplete(PlayCueSound);
            }

            if (UseColor)
            {
                foreach (var material in _materials)
                {
                    _cueSequence.Insert(0, material.DOColor(DesiredColor, _baseColorID, HalfLoopDuration).SetEase(ColorCurve));
                }
            }

            if (UseEmission)
            {
                foreach (var material in _materials)
                {
                    _cueSequence.Insert(0, material.DOColor(DesiredEmissionColor, _emissionColorID, HalfLoopDuration).SetEase(EmissionCurve));
                }
            }

            _cueSequence.SetLoops(TweenNumberOfLoops, LoopType.Yoyo);

            _cueSequence.onStepComplete += () => RemoveAudioCueFromFinalLoop(TweenNumberOfLoops);

            _cueSequence.onComplete += StartReturnSequence; // Once run out of loops, start the ReturnSequence.
        }


        public void RemoveAudioCueFromFinalLoop(int loops)
        {
            if (_cueSequence.CompletedLoops() == loops - 1)
            {
                _cueSequence.OnStepComplete(null); // Methods like these clear all previous calls. Use lowerCase if you want to add.
            }
        }


        [ContextMenu(nameof(StopCue))]
        public void StopCue()
        {
            StartReturnSequence();
        }


        public void StartReturnSequence()
        {
            if (Application.isPlaying == false)
            {
                return;
            }


            if (_cueSequence.IsActive() == false) // Return sequence can only start if the original CueSequence is currently playing
            {
                Debug.Log("Cuesequence is not active, cannot start return sequence");

                return;
            }

            if (_returnSequence.IsActive()) // Do not restart the ReturnSequence if already running.
            {
                Debug.Log("Cuesequence is active, cannot restart return sequence");

                return;
            }

            if (_cueSequence.CompletedLoops() % 2 != 0) // These are all the uneven loops, meaning that they are from max value to original / starting value.
            {
                ReturnSequenceDuration = HalfLoopDuration - _cueSequence.position; // DoTween always counts the cueSequence.position (duration timer in seconds) up, so in every 1 FULL loop, it counts from 0 to HalfLoopDuration. Therefore getting the corect time to get back to Starting Position is HalfLoopDuration - position
            }
            else
            {
                ReturnSequenceDuration = _cueSequence.position;
            }

            _cueSequence.Kill();

            BuildReturnSequence();
        }


        public void BuildReturnSequence()
        {
            _returnSequence = DOTween.Sequence();

            CueIsActive = false;

            if (UsePosition)
            {
                _returnSequence.Insert(0, transform.DOLocalMove(_originalPosition, ReturnSequenceDuration).SetEase(ReturnCurve));
            }

            if (UseRotation)
            {
                _returnSequence.Insert(0, transform.DOLocalRotate(_originalRotation.eulerAngles, ReturnSequenceDuration).SetEase(ReturnCurve));
            }

            if (UseScale)
            {
                _returnSequence.Insert(0, transform.DOScale(_originalScale, ReturnSequenceDuration).SetEase(ReturnCurve));
            }

            if (Renderers != null && UseColor)
            {
                for (var i = 0; i < _materials.Length; i++)
                {
                    _returnSequence.Insert(0, _materials[i].DOColor(_originalBaseColors[i], _baseColorID, ReturnSequenceDuration).SetEase(ReturnCurve));
                }
            }

            if (Renderers != null && UseEmission)
            {
                for (var i = 0; i < _materials.Length; i++)
                {
                    _returnSequence.Insert(0, _materials[i].DOColor(_originalEmissionColors[i], _emissionColorID, ReturnSequenceDuration).SetEase(ReturnCurve));
                }
            }

            if (UseStopSound)
            {
                _returnSequence.onComplete += PlayStopSound;
            }

            _returnSequence.onComplete += ApplyObjectOriginalSettings; // Lowercase onComplete allows stacking of methods. Uppercase OnComplete removes previous entries.

            _returnSequence.onComplete += () => EventOnStop?.Invoke(); // I sure hope this fires the event
        }


        public void PlayStopSound()
        {
            PlayClipAtPoint(StopClip);
        }


        public void ApplyObjectOriginalSettings()
        {
            if (Renderers != null)
            {
                for (var i = 0; i < _materials.Length; i++)
                {
                    _materials[i].SetColor(_baseColorID, _originalBaseColors[i]);
                    _materials[i].SetColor(_emissionColorID, _originalEmissionColors[i]);
                }
            }

            if (UseScale)
            {
                transform.localScale = _originalScale;
            }

            if (UsePosition)
            {
                transform.localPosition = _originalPosition;
            }

            if (UseRotation)
            {
                transform.localRotation = _originalRotation;
            }
        }


        /// <summary>
        ///     Called from Unity Events OnXRGrab: true, and OnXRRelease: false
        /// </summary>
        /// <param name="prevent"></param>
        public void PreventCueFromStarting(bool prevent)
        {
            PreventStartingCue = prevent;
        }


        private void OnDisable()
        {
            if (_cueSequence.IsActive())
            {
                _cueSequence.Kill();
            }

            if (_rescueSequence.IsActive())
            {
                _rescueSequence.Kill();
            }

            if (_returnSequence.IsActive())
            {
                _returnSequence.Kill();
            }
        }
    }
}
*/

