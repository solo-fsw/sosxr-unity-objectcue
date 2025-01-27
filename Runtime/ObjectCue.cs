using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using SOSXR.EditorTools;
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
        [Range(-1f, 10f)] [SerializeField] private float m_startDelay = -1;


        [Header("LOOPING AND RETURN")]
        [Tooltip("Loop duration in seconds (this is a full loop, so including the return to start values)")]
        [Range(0.1f, 10f)] [SerializeField] private float m_cueLoopDuration = 2.5f;
        [Tooltip("The amount of times this loop should repeat. -1 is infinite, 0 is do not loop")]
        [Range(-1, 100)] [SerializeField] private int m_numberOfLoops = -1;
        [Tooltip("In seconds. In case ReturnSequence is active when CueSequence is called again, this is the duration to which the object will transition back to original values, prior to starting new CueSequence")]
        [Range(0.1f, 5f)] [SerializeField] private float m_gracefulTransitionDuration = 0.5f;
        [SerializeField] private ReturnDurationType m_returnDurationType = ReturnDurationType.RemainingTimeInCueSequence;
        [Tooltip("Return duration in seconds: Once all loops are done / looping in stopped, how long does it take to transition back to original / starting values?")]
        [Range(0.1f, 10f)] [SerializeField] private float m_customReturnDuration = 1f;
        [Tooltip("A returnCurve starting low (0,0) and ending high (1,0.99f) seems to work well. When final value is set to 1 instead of 0.99f, flickering (in Emission) occurs.")]
        [SerializeField] private AnimationCurve m_returnCurve = new(new Keyframe(0, 0), new Keyframe(1, 0.99f));
        [SerializeField] private List<RendererSet> m_renderers = new();


        [Header("LOCAL TRANSFORM SETTINGS")]
        [SerializeField] private Vector3 m_addedLocalPosition;
        [SerializeField] private AnimationCurve m_positionCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));

        [Space(10)]
        [SerializeField] private Vector3 m_addedLocalRotation;
        [SerializeField] private AnimationCurve m_rotationCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));

        [Space(10)]
        [SerializeField] private Vector3 m_addedLocalScale;
        [SerializeField] private AnimationCurve m_scaleCurve = new(new Keyframe(0, 0), new Keyframe(1, 1));


        [Header("AUDIO SETTINGS")]
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private AudioClip m_cueClip;
        [Tooltip("This sound will be played at each 'halfway-point' of the loop: at start, once reached max values, upon returning to original / starting values. It will not be played at final rest (upon reaching origianl starting values, once loops are done / stopped).")]
        [SerializeField] private AudioClip m_halfWayClip;
        [Tooltip("Sound that will be played at very last reaching of the original values, at the very end of all the loops.")]
        [SerializeField] private AudioClip m_stopClip;

        [Header("UNITY EVENTS")]
        public UnityEvent EventOnStart;
        public UnityEvent EventOnStop;
        [SerializeField] [DisableEditing] private float m_position;


        private bool _preventStartingCue;
        private int _baseColorID = Shader.PropertyToID("_BaseColor");
        private int _emissionColorID = Shader.PropertyToID("_EmissionColor");
        private Sequence _cueSequence;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _originalScale;
        private float _remainingDurationInCueSequence;
        private Sequence _gracefulReturnSequence;
        private Sequence _returnSequence;
        private float _halfLoopDuration => m_cueLoopDuration / 2; // DoTween counts one way (from starting values to max values) as 1 loop, whereas I think is more logical to count a loop from 0 to full and back to 0
        private int _tweenNumberOfLoops => m_numberOfLoops >= 1 ? m_numberOfLoops * 2 : m_numberOfLoops; // DoTween counts one way (from 0 to full) as a loop, whereas I think is more logical to count a loop from 0 to full and back to 0


        private float _returnSequenceDuration
        {
            get => m_returnDurationType == ReturnDurationType.RemainingTimeInCueSequence ? _remainingDurationInCueSequence : m_customReturnDuration;

            set => _remainingDurationInCueSequence = value;
        }

        public bool PreventCueFromStarting { get; set; }
        public bool CueIsActive { get; private set; }


        private void OnValidate()
        {
            GetAudioSource();

            GetRenderers();
        }


        private void GetAudioSource()
        {
            if (m_audioSource != null)
            {
                return;
            }

            if (m_cueClip == null && m_halfWayClip == null && m_stopClip == null)
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
            if (m_renderers.Count != 0)
            {
                return;
            }

            InitialiseRenderers();
        }


        [ContextMenu(nameof(InitialiseRenderers))]
        public void InitialiseRenderers()
        {
            if (Application.isPlaying)
            {
                return;
            }

            var childRenderers = GetComponentsInChildren<Renderer>();
            m_renderers = new List<RendererSet>();

            if (childRenderers.Length == 0)
            {
                Debug.Log("No renderers found in children of: " + name);

                return;
            }

            foreach (var rend in childRenderers)
            {
                m_renderers.Add(new RendererSet());
                m_renderers[^1].Renderer = rend;

                if (rend.sharedMaterial == null)
                {
                    Debug.Log("No sharedMaterial found on: " + rend.name);

                    continue;
                }

                var baseColor = rend.sharedMaterial.GetColor(_baseColorID);
                m_renderers[^1].OriginalColor = baseColor;
                m_renderers[^1].DesiredColor = baseColor;

                var baseEmissive = rend.sharedMaterial.GetColor(_emissionColorID);
                baseEmissive *= 0;
                m_renderers[^1].OriginalEmissionColor = baseEmissive;
                m_renderers[^1].DesiredEmissionColor = baseEmissive;

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
            if (m_startDelay < 0)
            {
                return;
            }

            Invoke(nameof(StartCue), m_startDelay);
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


        private void Update()
        {
            if (_cueSequence != null && _cueSequence.IsActive())
            {
                m_position = _cueSequence.position;
            }
        }


        [ContextMenu(nameof(StartCue))]
        public void StartCue()
        {
            if (!Application.isPlaying)
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
                CreateCueLoop();
            }
        }


        private void CreateCueLoop()
        {
            if (PreventCueFromStarting)
            {
                Debug.Log("PreventStartingCue is active, cannot start cue sequence");

                return;
            }

            _cueSequence = DOTween.Sequence();

            CueIsActive = true;

            PlayClip(m_cueClip);

            if (m_addedLocalPosition != Vector3.zero)
            {
                var pos = _originalPosition + m_addedLocalPosition;
                _cueSequence.Insert(0, transform.DOLocalMove(pos, _halfLoopDuration).SetEase(m_positionCurve));
            }

            if (m_addedLocalRotation != Vector3.zero)
            {
                var rot = _originalRotation.eulerAngles + m_addedLocalRotation;
                _cueSequence.Insert(0, transform.DOLocalRotate(rot, _halfLoopDuration).SetEase(m_rotationCurve));
            }

            if (m_addedLocalScale != Vector3.zero)
            {
                var scale = _originalScale + m_addedLocalScale;
                _cueSequence.Insert(0, transform.DOScale(scale, _halfLoopDuration).SetEase(m_scaleCurve));
            }

            _cueSequence.InsertCallback(_halfLoopDuration / 2, () => PlayClip(m_halfWayClip));

            _cueSequence.onStepComplete += () => PlayClip(m_cueClip);

            foreach (var rend in m_renderers.Where(rend => rend.Renderer.sharedMaterial != null))
            {
                if (rend.DesiredColor != rend.OriginalColor)
                {
                    _cueSequence.Insert(0, rend.Renderer.sharedMaterial.DOColor(rend.DesiredColor, _baseColorID, _halfLoopDuration).SetEase(rend.ColorCurve));
                }

                if (rend.DesiredEmissionColor != rend.OriginalEmissionColor)
                {
                    _cueSequence.Insert(0, rend.Renderer.sharedMaterial.DOColor(rend.DesiredEmissionColor, _emissionColorID, _halfLoopDuration).SetEase(rend.EmissionCurve));
                }
            }

            _cueSequence.SetLoops(_tweenNumberOfLoops, LoopType.Yoyo);

            _cueSequence.onStepComplete += () => RemoveAudioCueFromFinalLoop(_tweenNumberOfLoops);

            _cueSequence.onComplete += StopCue; // Once run out of loops, start the ReturnSequence.
        }


        private void RemoveAudioCueFromFinalLoop(int loops)
        {
            if (_cueSequence.CompletedLoops() != loops - 1)
            {
                return;
            }

            _cueSequence.OnStepComplete(null); // Methods like these clear all previous calls. Use lowerCase if you want to add.
        }


        private void CreateGracefulTransitionBetweenReturnSequenceAndCueSequence()
        {
            _gracefulReturnSequence = DOTween.Sequence();

            foreach (var rend in m_renderers.Where(rend => rend.Renderer.sharedMaterial != null))
            {
                _gracefulReturnSequence.Insert(0, rend.Renderer.sharedMaterial.DOColor(rend.OriginalColor, _baseColorID, m_gracefulTransitionDuration).SetEase(m_returnCurve));
            }

            _gracefulReturnSequence.Insert(0, transform.DOScale(_originalScale, m_gracefulTransitionDuration).SetEase(m_returnCurve));
            _gracefulReturnSequence.Insert(0, transform.DOLocalMove(_originalPosition, m_gracefulTransitionDuration).SetEase(m_returnCurve));
            _gracefulReturnSequence.Insert(0, transform.DOLocalRotate(_originalRotation.eulerAngles, m_gracefulTransitionDuration).SetEase(m_returnCurve));

            _gracefulReturnSequence.onComplete += () => PlayClip(m_cueClip);
        }


        private void PlayClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            GetAudioSource();

            m_audioSource.clip = clip;
            m_audioSource.Play();
        }


        [ContextMenu(nameof(StopCue))]
        public void StopCue()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_cueSequence.IsActive()) // Return sequence can only start if the original CueSequence is currently playing
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
                _returnSequenceDuration = _halfLoopDuration - _cueSequence.position; // DoTween always counts the cueSequence.position (duration timer in seconds) up, so in every 1 FULL loop, it counts from 0 to HalfLoopDuration. Therefore getting the corect time to get back to Starting Position is HalfLoopDuration - position
            }
            else
            {
                _returnSequenceDuration = _cueSequence.position;
            }

            _cueSequence.Kill();

            _returnSequence = DOTween.Sequence();

            if (m_addedLocalPosition != Vector3.zero)
            {
                _returnSequence.Insert(0, transform.DOLocalMove(_originalPosition, _returnSequenceDuration).SetEase(m_returnCurve));
            }

            if (m_addedLocalRotation != Vector3.zero)
            {
                _returnSequence.Insert(0, transform.DOLocalRotate(_originalRotation.eulerAngles, _returnSequenceDuration).SetEase(m_returnCurve));
            }

            if (m_addedLocalScale != Vector3.zero)
            {
                _returnSequence.Insert(0, transform.DOScale(_originalScale, _returnSequenceDuration).SetEase(m_returnCurve));
            }

            foreach (var rend in m_renderers.Where(rend => rend.Renderer.sharedMaterial != null))
            {
                if (rend.DesiredColor != rend.OriginalColor)
                {
                    _returnSequence.Insert(0, rend.Renderer.sharedMaterial.DOColor(rend.OriginalColor, _baseColorID, _returnSequenceDuration).SetEase(m_returnCurve));
                }

                if (rend.DesiredEmissionColor != rend.OriginalEmissionColor)
                {
                    _returnSequence.Insert(0, rend.Renderer.sharedMaterial.DOColor(rend.OriginalEmissionColor, _emissionColorID, _returnSequenceDuration).SetEase(m_returnCurve));
                }
            }

            _returnSequence.onComplete += OnReturnComplete; // Lowercase onComplete allows stacking of methods. Uppercase OnComplete removes previous entries.
        }


        private void OnReturnComplete()
        {
            PlayClip(m_stopClip);

            ApplyObjectOriginalSettings();

            CueIsActive = false;

            EventOnStop?.Invoke();
        }


        private void ApplyObjectOriginalSettings()
        {
            if (m_addedLocalScale != Vector3.zero)
            {
                transform.localScale = _originalScale;
            }

            if (m_addedLocalPosition != Vector3.zero)
            {
                transform.localPosition = _originalPosition;
            }

            if (m_addedLocalRotation != Vector3.zero)
            {
                transform.localRotation = _originalRotation;
            }

            foreach (var rend in m_renderers.Where(rend => rend.Renderer.sharedMaterial != null))
            {
                rend.Renderer.sharedMaterial.SetColor(_baseColorID, rend.OriginalColor);
                rend.Renderer.sharedMaterial.SetColor(_emissionColorID, rend.OriginalEmissionColor);
            }
        }


        private void OnDisable()
        {
            if (_cueSequence.IsActive())
            {
                _cueSequence.Kill();
            }

            if (_gracefulReturnSequence.IsActive())
            {
                _gracefulReturnSequence.Kill();
            }

            if (_returnSequence.IsActive())
            {
                _returnSequence.Kill();
            }
        }
    }
}