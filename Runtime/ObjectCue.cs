using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;


namespace SOSXR.ObjectCue
{
    public enum ReturnDurationType
    {
        RemainingTimeInCueSequence,
        CustomDuration
    }


    [Serializable]
    public class RendererSet
    {
        public Renderer Renderer;
        public Material SharedMaterial;
        [Space(10)]
        public Color OriginalColor;
        public AnimationCurve ColorCurve = new(new Keyframe(0, 0.05f), new Keyframe(1, 1f));
        public Color DesiredColor;
        [Space(10)]
        public Color OriginalEmissionColor;
        [Tooltip("Emission curve should probably never hit 0, because then flickering occurs.")]
        public AnimationCurve EmissionCurve = new(new Keyframe(0, 0.05f), new Keyframe(1, 0.35f));
        [Tooltip("DesiredEmissionColor should have a non-zero alpha value, otherwise it won't work.")]
        public Color DesiredEmissionColor;
    }


    [Serializable]
    public class ObjectCue : MonoBehaviour
    {
        [Header("AUTOSTART")]
        [Tooltip("In seconds. If -1, the CueSequence will not start automatically.")]
        [Range(-1f, 10f)] public float StartDelay = -1;

        [Header("LOOPING AND RETURN")]
        [Tooltip("Loop duration in seconds (this is a full loop, so including the return to start values)")]
        [Range(0.1f, 10f)] public float CueLoopDuration = 2.5f;
        [Tooltip("The amount of times this loop should repeat. -1 is infinite, 0 is do not loop")]
        [Range(-1, 100)] public int NumberOfLoops = -1;
        [Tooltip("In seconds. In case ReturnSequence is active when CueSequence is called again, this is the duration to which the object will transition back to original values, prior to starting new CueSequence")]
        [Range(0.1f, 5f)] public float GracefulTransitionDuration = 0.5f;
        public ReturnDurationType ReturnDurationType = ReturnDurationType.RemainingTimeInCueSequence;
        [Tooltip("Return duration in seconds: Once all loops are done / looping in stopped, how long does it take to transition back to original / starting values?")]
        [Range(0.1f, 10f)] public float CustomReturnDuration = 1f;
        [Tooltip("A returnCurve starting low (0,0) and ending high (1,0.99f) seems to work well. When final value is set to 1 instead of 0.99f, flickering (in Emission) occurs.")]
        public AnimationCurve ReturnCurve = new(new Keyframe(0, 0), new Keyframe(1, 0.99f));

        public List<RendererSet> Renderers = new();

        [Header("LOCAL TRANSFORM SETTINGS")]
        public Vector3 AddedLocalPosition;
        public AnimationCurve PositionCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));

        [Space(10)]
        public Vector3 AddedLocalRotation;
        public AnimationCurve RotationCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));

        [Space(10)]
        public Vector3 AddedLocalScale;
        public AnimationCurve ScaleCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));

        [Header("AUDIO SETTINGS")]
        [SerializeField] private AudioSource m_audioSource;
        public AudioClip CueClip;
        [Tooltip("This sound will be played at each 'halfway-point' of the loop: at start, once reached max values, upon returning to original / starting values. It will not be played at final rest (upon reaching origianl starting values, once loops are done / stopped).")]
        public bool PlayHalfWay;
        [Tooltip("Sound that will be played at very last reaching of the original values, at the very end of all the loops.")]
        public AudioClip StopClip;

        [Header("UNITY EVENTS")]
        public UnityEvent EventOnStart;
        public UnityEvent EventOnStop;

        private bool _preventStartingCue;
        private int _baseColorID = Shader.PropertyToID("_BaseColor");
        private int _emissionColorID = Shader.PropertyToID("_EmissionColor");
        private Sequence _cueSequence;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _originalScale;
        private float _remainingDurationInCueSequence;
        private Sequence _rescueSequence;
        private Sequence _returnSequence;
        public bool CueIsActive { get; set; }
        public float HalfLoopDuration => CueLoopDuration / 2; // DoTween counts one way (from starting values to max values) as 1 loop, whereas I think is more logical to count a loop from 0 to full and back to 0
        public int TweenNumberOfLoops => NumberOfLoops >= 1 ? NumberOfLoops * 2 : NumberOfLoops; // DoTween counts one way (from 0 to full) as a loop, whereas I think is more logical to count a loop from 0 to full and back to 0


        public float ReturnSequenceDuration
        {
            get => ReturnDurationType == ReturnDurationType.RemainingTimeInCueSequence ? _remainingDurationInCueSequence : CustomReturnDuration;

            private set => _remainingDurationInCueSequence = value;
        }


        private void OnValidate()
        {
            GetAudioSource();

            GetRenderers();
        }


        private void GetAudioSource()
        {
            if (CueClip == null && StopClip == null)
            {
                return;
            }

            if (m_audioSource != null)
            {
                return;
            }

            m_audioSource = GetComponent<AudioSource>();

            if (m_audioSource != null)
            {
                return;
            }

            m_audioSource = gameObject.AddComponent<AudioSource>();
            m_audioSource.spatialize = true;
            m_audioSource.spatialBlend = 1;
            m_audioSource.minDistance = 0;
            m_audioSource.maxDistance = 20;
            m_audioSource.rolloffMode = AudioRolloffMode.Linear;
            m_audioSource.playOnAwake = false;
            m_audioSource.loop = false;
        }


        private void GetRenderers()
        {
            if (Renderers != null && Renderers.Count != 0)
            {
                return;
            }

            var renderers = GetComponentsInChildren<Renderer>();

            foreach (var rend in renderers)
            {
                var baseEmissive = rend.sharedMaterial.GetColor(_emissionColorID);
                baseEmissive *= 0;
                var baseColor = rend.sharedMaterial.GetColor(_baseColorID);

                Renderers?.Add(new RendererSet
                {
                    Renderer = rend,
                    SharedMaterial = rend.sharedMaterial,
                    OriginalColor = baseColor,
                    DesiredColor = baseColor,
                    OriginalEmissionColor = baseEmissive,
                    DesiredEmissionColor = baseEmissive
                });

                rend.sharedMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                rend.sharedMaterial.EnableKeyword("_EMISSION");
                rend.sharedMaterial.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
        }


        private void Awake()
        {
            var trans = transform;
            _originalScale = trans.localScale;
            _originalPosition = trans.localPosition;
            _originalRotation = trans.localRotation;
        }


        private void OnEnable()
        {
            if (StartDelay < 0)
            {
                return;
            }

            Invoke(nameof(StartCue), StartDelay);
        }


        [ContextMenu(nameof(ToggleCue))]
        public void ToggleCue()
        {
            ToggleCue(!CueIsActive);
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

            if (_cueSequence.IsActive()) // Don't start CueSequence if already playing
            {
                Debug.Log("Cue sequence is active, cannot restart cue sequence");

                return;
            }

            EventOnStart?.Invoke();

            if (_returnSequence.IsActive()) // Makes sure this CueSequence can always run, even when ReturnSequence is currently active.
            {
                _returnSequence.Kill();

                CreateGracefulTransitionBetweenReturnSequenceAndCueSequence();
            }
            else
            {
                if (PlayHalfWay == false)
                {
                    PlayCueSound(); // To ensure sound is played at start of loop as well as end.
                }

                CreateCueLoop();
            }
        }


        private void CreateGracefulTransitionBetweenReturnSequenceAndCueSequence()
        {
            _rescueSequence = DOTween.Sequence();

            for (var i = 0; i < Renderers.Count; i++)
            {
                _rescueSequence.Insert(0, Renderers[i].Renderer.material.DOColor(Renderers[i].OriginalColor, _baseColorID, GracefulTransitionDuration).SetEase(ReturnCurve));
            }

            _rescueSequence.Insert(0, transform.DOScale(_originalScale, GracefulTransitionDuration).SetEase(ReturnCurve));
            _rescueSequence.Insert(0, transform.DOLocalMove(_originalPosition, GracefulTransitionDuration).SetEase(ReturnCurve));
            _rescueSequence.Insert(0, transform.DOLocalRotate(_originalRotation.eulerAngles, GracefulTransitionDuration).SetEase(ReturnCurve));

            _rescueSequence.onComplete += CreateCueLoop;
        }


        private void PlayCueSound()
        {
            PlayClipAtPoint(CueClip);
        }


        private void PlayClipAtPoint(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (m_audioSource == null)
            {
                Debug.LogWarning("No AudioSource found, cannot play clip: " + clip.name);

                return;
            }

            m_audioSource.clip = clip;
            m_audioSource.Play();
        }


        private void CreateCueLoop()
        {
            if (_preventStartingCue)
            {
                return;
            }

            _cueSequence = DOTween.Sequence();

            CueIsActive = true;

            if (AddedLocalPosition != Vector3.zero)
            {
                var pos = _originalPosition + AddedLocalPosition;
                _cueSequence.Insert(0, transform.DOLocalMove(pos, HalfLoopDuration).SetEase(PositionCurve));
            }

            if (AddedLocalRotation != Vector3.zero)
            {
                var rot = _originalRotation.eulerAngles + AddedLocalRotation;
                _cueSequence.Insert(0, transform.DOLocalRotate(rot, HalfLoopDuration).SetEase(RotationCurve));
            }

            if (AddedLocalScale != Vector3.zero)
            {
                var scale = _originalScale + AddedLocalScale;
                _cueSequence.Insert(0, transform.DOScale(scale, HalfLoopDuration).SetEase(ScaleCurve));
            }

            if (PlayHalfWay)
            {
                _cueSequence.InsertCallback(HalfLoopDuration / 2, PlayCueSound);
            }
            else
            {
                _cueSequence.OnStepComplete(PlayCueSound);
            }

            foreach (var rend in Renderers)
            {
                if (rend.DesiredColor != rend.OriginalColor)
                {
                    _cueSequence.Insert(0, rend.Renderer.material.DOColor(rend.DesiredColor, _baseColorID, HalfLoopDuration).SetEase(rend.ColorCurve));
                }

                if (rend.DesiredEmissionColor != rend.OriginalEmissionColor)
                {
                    _cueSequence.Insert(0, rend.Renderer.material.DOColor(rend.DesiredEmissionColor, _emissionColorID, HalfLoopDuration).SetEase(rend.EmissionCurve));
                }
            }

            _cueSequence.SetLoops(TweenNumberOfLoops, LoopType.Yoyo);

            _cueSequence.onStepComplete += () => RemoveAudioCueFromFinalLoop(TweenNumberOfLoops);

            _cueSequence.onComplete += StartReturnSequence; // Once run out of loops, start the ReturnSequence.
        }


        private void RemoveAudioCueFromFinalLoop(int loops)
        {
            if (_cueSequence.CompletedLoops() != loops - 1)
            {
                return;
            }

            _cueSequence.OnStepComplete(null); // Methods like these clear all previous calls. Use lowerCase if you want to add.
        }


        [ContextMenu(nameof(StopCue))]
        public void StopCue()
        {
            StartReturnSequence();
        }


        private void StartReturnSequence()
        {
            if (Application.isPlaying == false)
            {
                return;
            }

            if (_cueSequence.IsActive() == false) // Return sequence can only start if the original CueSequence is currently playing
            {
                Debug.Log("Sequence is not active, cannot start return sequence");

                return;
            }

            if (_returnSequence.IsActive()) // Do not restart the ReturnSequence if already running.
            {
                Debug.Log("Sequence is active, cannot restart return sequence");

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


        private void BuildReturnSequence()
        {
            _returnSequence = DOTween.Sequence();

            if (AddedLocalPosition != Vector3.zero)
            {
                _returnSequence.Insert(0, transform.DOLocalMove(_originalPosition, ReturnSequenceDuration).SetEase(ReturnCurve));
            }

            if (AddedLocalRotation != Vector3.zero)
            {
                _returnSequence.Insert(0, transform.DOLocalRotate(_originalRotation.eulerAngles, ReturnSequenceDuration).SetEase(ReturnCurve));
            }

            if (AddedLocalScale != Vector3.zero)
            {
                _returnSequence.Insert(0, transform.DOScale(_originalScale, ReturnSequenceDuration).SetEase(ReturnCurve));
            }

            foreach (var rend in Renderers)
            {
                if (rend.DesiredColor != rend.OriginalColor)
                {
                    _returnSequence.Insert(0, rend.Renderer.material.DOColor(rend.OriginalColor, _baseColorID, ReturnSequenceDuration).SetEase(ReturnCurve));
                }

                if (rend.DesiredEmissionColor != rend.OriginalEmissionColor)
                {
                    _returnSequence.Insert(0, rend.Renderer.material.DOColor(rend.OriginalEmissionColor, _emissionColorID, ReturnSequenceDuration).SetEase(ReturnCurve));
                }
            }

            _returnSequence.onComplete += OnReturnComplete; // Lowercase onComplete allows stacking of methods. Uppercase OnComplete removes previous entries.
        }


        private void OnReturnComplete()
        {
            PlayClipAtPoint(StopClip);

            ApplyObjectOriginalSettings();

            CueIsActive = false;
            EventOnStop?.Invoke();
        }


        private void ApplyObjectOriginalSettings()
        {
            foreach (var rend in Renderers)
            {
                rend.SharedMaterial.SetColor(_baseColorID, rend.OriginalColor);
                rend.SharedMaterial.SetColor(_emissionColorID, rend.OriginalEmissionColor);
            }

            if (AddedLocalScale != Vector3.zero)
            {
                transform.localScale = _originalScale;
            }

            if (AddedLocalPosition != Vector3.zero)
            {
                transform.localPosition = _originalPosition;
            }

            if (AddedLocalRotation != Vector3.zero)
            {
                transform.localRotation = _originalRotation;
            }
        }


        public void PreventCueFromStarting(bool prevent, bool stop)
        {
            _preventStartingCue = prevent;

            if (prevent && stop)
            {
                StopCue();
            }
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