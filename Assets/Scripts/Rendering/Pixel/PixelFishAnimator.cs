using UnityEngine;

namespace Aquarium.PixelArt
{
    /// <summary>
    /// Drives per-part frame indexes on a <see cref="FishCompositor"/>.
    /// Reads current swim speed from <see cref="FishAnimator"/> so the
    /// swim cycle (body undulation, tail wag, fin flutter) speeds up while
    /// dashing and slows during pauses. Eye blinks fire on a Poisson timer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FishCompositor))]
    public class PixelFishAnimator : MonoBehaviour
    {
        [Tooltip("Multiplier on the swim FPS. Per-fish FPS = settings.baseSwimFps * speedScale * speedFactor.")]
        [SerializeField] float fpsScale = 1f;

        [Tooltip("How quickly the perceived speed catches up to true swim velocity (1/seconds). Higher = snappier.")]
        [SerializeField] float speedSmoothing = 6f;

        [Tooltip("Min speed factor — fish never freeze completely (idle wobble).")]
        [Range(0.05f, 1f)] [SerializeField] float minSpeedFactor = 0.25f;

        FishCompositor _compositor;
        FishAnimator _swimmer;

        float _bodyPhase;
        float _tailPhase;
        float _finPhase;
        float _smoothedSpeedFactor = 1f;

        float _blinkTimer;
        bool _blinking;
        int _blinkFrameIndex;

        System.Random _rng;

        void Awake()
        {
            _compositor = GetComponent<FishCompositor>();
            _swimmer = GetComponent<FishAnimator>();
            int seed = Mathf.Abs(GetInstanceID()) ^ Time.frameCount * 31;
            _rng = new System.Random(seed);
            _blinkTimer = NextBlinkInterval();
        }

        void Update()
        {
            if (_compositor == null || _compositor.Settings == null)
                return;

            PixelArtSettings s = _compositor.Settings;
            float dt = Application.isPlaying ? Time.deltaTime : 0f;

            // Smooth speed → frame rate. FishAnimator doesn't expose velocity
            // directly, so we estimate from transform displacement over dt.
            float instantFactor = EstimateSpeedFactor();
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.0001f, speedSmoothing) * dt);
            _smoothedSpeedFactor = Mathf.Lerp(_smoothedSpeedFactor, instantFactor, dt > 0f ? blend : 1f);
            float effectiveFactor = Mathf.Max(minSpeedFactor, _smoothedSpeedFactor);

            float baseFps = Mathf.Max(0.5f, s.baseSwimFps);
            float fps = baseFps * fpsScale * effectiveFactor;

            // Advance per-part phases at slightly different rates so fins +
            // tail + body don't visually sync up.
            _bodyPhase += dt * fps;
            _tailPhase += dt * fps * 1.4f;
            _finPhase  += dt * fps * 1.1f;

            ApplyFrame(PixelPartType.Body, Mathf.FloorToInt(_bodyPhase));
            ApplyFrame(PixelPartType.Tail, Mathf.FloorToInt(_tailPhase));
            ApplyFrame(PixelPartType.PectoralFin, Mathf.FloorToInt(_finPhase));
            ApplyFrame(PixelPartType.DorsalFin, Mathf.FloorToInt(_finPhase));

            UpdateBlink(dt);
        }

        void ApplyFrame(PixelPartType slot, int frameIndex)
        {
            FishPart part = _compositor.GetActivePart(slot);
            if (part == null || part.FrameCount == 0)
                return;
            _compositor.SetSlotFrame(slot, frameIndex);
        }

        // FishAnimator drives transform.position directly. We sample the
        // displacement to estimate speed without coupling to its internal
        // velocity field.
        Vector3 _prevPos;
        bool _prevPosInit;

        float EstimateSpeedFactor()
        {
            Vector3 pos = transform.position;
            if (!_prevPosInit)
            {
                _prevPos = pos;
                _prevPosInit = true;
                return 1f;
            }
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = (pos - _prevPos).magnitude / dt;
            _prevPos = pos;

            // FishAnimator's swimSpeedBase ≈ 2.2; map 0..4 to 0.2..1.6 factor.
            return Mathf.Clamp(speed / 2.2f, 0f, 2.5f);
        }

        // ------------------------------------------------------------------
        // Eye blink: a Poisson-ish timer triggers a one-shot blink that runs
        // through the eye part's frame strip in real time, then snaps back.
        // ------------------------------------------------------------------

        void UpdateBlink(float dt)
        {
            FishPart eye = _compositor.GetActivePart(PixelPartType.Eye);
            if (eye == null || eye.FrameCount <= 1)
            {
                _compositor.SetSlotFrame(PixelPartType.Eye, 0);
                return;
            }

            if (_blinking)
            {
                _blinkTimer -= dt;
                int frame = Mathf.Clamp(_blinkFrameIndex, 0, eye.FrameCount - 1);
                _compositor.SetSlotFrame(PixelPartType.Eye, frame);

                // Step through close → open → done.
                if (_blinkTimer <= 0f)
                {
                    _blinkFrameIndex++;
                    _blinkTimer = 0.05f; // 50ms per blink frame
                    if (_blinkFrameIndex >= eye.FrameCount * 2 - 1)
                    {
                        _blinking = false;
                        _blinkFrameIndex = 0;
                        _compositor.SetSlotFrame(PixelPartType.Eye, 0);
                        _blinkTimer = NextBlinkInterval();
                    }
                }
                return;
            }

            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blinking = true;
                _blinkFrameIndex = 0;
                _blinkTimer = 0.05f;
            }
        }

        float NextBlinkInterval()
        {
            PixelArtSettings s = _compositor != null ? _compositor.Settings : null;
            float avg = s != null ? s.averageBlinkInterval : 4.5f;
            // Exponential distribution (Poisson process) with avg = avg.
            float u = (_rng != null) ? (float)(1.0 - _rng.NextDouble()) : Random.value;
            return -Mathf.Log(Mathf.Max(0.001f, u)) * Mathf.Max(0.5f, avg);
        }
    }
}
