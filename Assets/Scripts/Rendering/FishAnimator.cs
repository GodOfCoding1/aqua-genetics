using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural swim motion driven by <c>swim_style</c>, <c>temperament</c>, <c>school_tendency</c>.
/// Five archetypes, each meant to read instantly as a personality trait:
///   0 Cruiser   — calm horizontal wanderer with smooth wide turns
///   1 Wiggler   — energetic side-to-side body wave with strong amplitude
///   2 Darter    — twitchy pause-and-burst skittish swimmer
///   3 Patroller — disciplined back-and-forth between two endpoints
///   4 Acrobat   — long horizontal travel punctuated by full barrel-roll loops
/// </summary>
[DisallowMultipleComponent]
public class FishAnimator : MonoBehaviour
{
    static readonly List<FishAnimator> Instances = new List<FishAnimator>(32);

    [SerializeField] Vector2 tankExtents = new Vector2(10.5f, 5.25f);
    [SerializeField] float swimSpeedBase = 2.2f;

    FishData _fish;
    GeneLibrary _geneLib;
    System.Random _rng;
    int _hashSeed;
    float _seedA, _seedB;

    Vector2 _velocity;
    float _facing = 1f;
    float _facingMomentum;
    float _wobblePhase;

    // Darter (style 2) — pause/twitch/burst.
    float _darterIdleTimer;
    float _darterDashTimer;
    bool _darterIsDashing;
    Vector2 _darterDashDir;
    float _darterTwitchTimer;
    Vector2 _darterTwitchVel;

    // Patroller (style 3) — two-point patrol with pauses.
    bool _patrolInit;
    Vector2 _patrolA, _patrolB;
    int _patrolToB;
    float _patrolPauseTimer;

    // Acrobat (style 4) — travel/loop state machine.
    int _acroPhase;
    float _acroTravelTimer;
    float _acroLoopAngle;
    Vector2 _acroLoopFwd;
    float _acroLoopSign;
    float _acroLoopDuration;

    public FishData BoundFish => _fish;

    void OnEnable()
    {
        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    void OnDisable()
    {
        Instances.Remove(this);
    }

    public void Bind(FishData fish, GeneLibrary geneLib)
    {
        _fish = fish;
        _geneLib = geneLib;

        int seed = 17;
        if (fish?.fishId != null)
        {
            foreach (char c in fish.fishId)
                seed = seed * 31 + c;
        }

        _hashSeed = unchecked(seed ^ GetEntityId().GetHashCode());
        _rng = new System.Random(_hashSeed);
        _seedA = ((_hashSeed & 0xFFFF) % 977) * 0.011f;
        _seedB = (((_hashSeed >> 16) & 0xFFFF) % 991) * 0.013f;

        _darterIdleTimer = RandRange(0.5f, 2f);
        _darterTwitchTimer = RandRange(0.2f, 0.6f);
        _darterDashTimer = 0f;
        _darterIsDashing = false;
        _patrolInit = false;
        _acroPhase = 0;
        _acroTravelTimer = RandRange(1.5f, 5f); // staggered initial loop times across fish
        _acroLoopFwd = new Vector2(_facing, 0f);

        RefreshPhenotypes();
    }

    void Update()
    {
        if (_fish == null || _geneLib == null || !_fish.isAlive)
            return;

        float swimStyle = P("swim_style");
        int style = Mathf.Clamp(Mathf.RoundToInt(swimStyle), 0, 4);

        float bodySize = P("body_size");
        float speedScale = Mathf.Lerp(0.75f, 1.35f, Mathf.InverseLerp(0.4f, 2f, bodySize));

        float temperament = P("temperament");
        float aggression = Mathf.InverseLerp(-1f, 1f, temperament);

        float school = P("school_tendency");
        Vector2 cohesion = AccumulateSchooling(school);

        Vector2 steerEdge = AvoidTankEdges();
        Vector2 steerSocial = SteerTowardNeighbors(aggression);

        Vector2 accel = cohesion * Mathf.Clamp01(school + 0.15f);
        accel += steerEdge * (1f - aggression * 0.35f);
        accel += steerSocial;

        float dt = Time.deltaTime;
        float speed = swimSpeedBase * speedScale;

        ApplySwimStyle(style, speed, temperament, accel, dt);

        UpdateFacingAndFlip();
        transform.position += (Vector3)_velocity * dt;
    }

    void RefreshPhenotypes()
    {
        // Reserved for keyed animation curves that should not read genome every frame.
    }

    float P(string id)
    {
        GeneDefinition def = _geneLib.GetGene(id);
        if (def == null || _fish?.genome == null)
            return 0f;
        return _fish.genome.GetPhenotype(id, def);
    }

    float RandRange(float a, float b)
    {
        return (float)(_rng.NextDouble() * (b - a) + a);
    }

    Vector2 AvoidTankEdges()
    {
        Vector2 p = transform.position;
        Vector2 steer = Vector2.zero;
        float margin = Mathf.Max(tankExtents.x, tankExtents.y) * 0.12f;

        if (p.x > tankExtents.x - margin)
            steer.x -= 1f;
        if (p.x < -tankExtents.x + margin)
            steer.x += 1f;
        if (p.y > tankExtents.y - margin)
            steer.y -= 1f;
        if (p.y < -tankExtents.y + margin)
            steer.y += 1f;

        return steer.normalized;
    }

    Vector2 SteerTowardNeighbors(float aggression)
    {
        if (aggression < 0.08f || Instances.Count < 2)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        int count = 0;
        Vector2 me = transform.position;

        foreach (FishAnimator other in Instances)
        {
            if (other == null || other == this)
                continue;
            if (_fish == null || other.BoundFish?.isAlive != true || other.BoundFish?.genome == null)
                continue;

            if (other.BoundFish.fishId == _fish.fishId)
                continue;

            GeneDefinition td = other._geneLib != null ? other._geneLib.GetGene("temperament") : null;
            float otherTemper = td != null && other.BoundFish.genome != null
                ? other.BoundFish.genome.GetPhenotype("temperament", td)
                : 0f;

            if (otherTemper < 0.2f && aggression > 0.5f)
            {
                Vector2 away = me - (Vector2)other.transform.position;
                if (away.sqrMagnitude > 0.001f && away.magnitude < 3f)
                {
                    sum += away.normalized;
                    count++;
                }
                continue;
            }

            Vector2 to = (Vector2)other.transform.position - me;
            if (to.sqrMagnitude < 36f && to.sqrMagnitude > 0.001f)
            {
                sum += to.normalized;
                count++;
            }
        }

        if (count == 0)
            return Vector2.zero;

        return (sum / count).normalized * Mathf.Lerp(0.35f, 1.1f, aggression);
    }

    Vector2 AccumulateSchooling(float schoolTendency)
    {
        if (schoolTendency <= 0.5f || Instances.Count < 2)
            return Vector2.zero;

        Vector2 me = transform.position;
        GeneDefinition sd = _geneLib.GetGene("school_tendency");

        Vector2 center = Vector2.zero;
        Vector2 avgVel = Vector2.zero;
        int n = 0;

        foreach (FishAnimator other in Instances)
        {
            if (other == null || other == this)
                continue;

            if (other.BoundFish?.isAlive != true || other.BoundFish.genome == null || other._geneLib == null)
                continue;

            if (other.BoundFish.fishId == _fish.fishId)
                continue;

            GeneDefinition osd = other._geneLib.GetGene("school_tendency");
            if (osd == null)
                continue;

            float st = other.BoundFish.genome.GetPhenotype("school_tendency", osd);
            if (st <= 0.5f)
                continue;

            float d = Vector2.Distance(me, other.transform.position);
            if (!(d > 0.001f && d < 10f))
                continue;

            center += (Vector2)other.transform.position;
            avgVel += other._velocity;
            n++;

            Vector2 sep = me - (Vector2)other.transform.position;
            if (sep.sqrMagnitude < 1f && sep.sqrMagnitude > 0.0001f)
                avgVel -= sep.normalized * 4f * (1f / (sep.magnitude + 0.05f));
        }

        if (n == 0)
            return Vector2.zero;

        center /= n;
        avgVel /= n;

        Vector2 cohesion = (center - me) * 0.35f * schoolTendency;
        Vector2 separation = avgVel.normalized * 0.08f;

        Vector2 align = avgVel.normalized * 0.15f;

        return cohesion + align + separation;
    }

    void ApplySwimStyle(int style, float speed, float temperament, Vector2 accel, float dt)
    {
        float t01 = Mathf.InverseLerp(-1f, 1f, temperament);

        switch (style)
        {
            case 0: ApplyCruiser(speed, t01, accel, dt); break;
            case 1: ApplyWiggler(speed, t01, accel, dt); break;
            case 2: ApplyDarter(speed, t01, accel, dt); break;
            case 3: ApplyPatroller(speed, t01, accel, dt); break;
            default: ApplyAcrobat(speed, t01, accel, dt); break;
        }

        // Frame-rate-independent atmospheric damping. Calm fish (low temperament) slow down
        // a bit faster, aggressive fish hold momentum.
        float dampPerSec = Mathf.Lerp(1.3f, 0.35f, t01);
        _velocity *= Mathf.Exp(-dampPerSec * dt);

        // Speed cap is mode/phase aware: dashes and loops get headroom.
        float cap = speed * 1.6f;
        if (style == 2 && _darterIsDashing) cap = speed * 5.0f;
        else if (style == 4 && _acroPhase == 1) cap = speed * 2.3f;
        if (_velocity.magnitude > cap) _velocity = _velocity.normalized * cap;

        // Edge-avoidance bump.
        _velocity += accel.normalized * (speed * 0.5f * dt);
        if (_velocity.magnitude > cap) _velocity = _velocity.normalized * cap;

        ClampToTankReflect();
    }

    /// <summary>0 — Cruiser: calm long-distance horizontal swimmer with smooth Perlin heading.</summary>
    void ApplyCruiser(float speed, float t01, Vector2 accel, float dt)
    {
        if (_velocity.sqrMagnitude < 1e-4f)
            _velocity = new Vector2(_facing * speed * 0.5f, 0f);

        // Heading wanders slowly. Vertical component compressed so the fish stays a horizontal swimmer.
        float nx = Mathf.PerlinNoise(Time.time * 0.07f + _seedA, _seedB) - 0.5f;
        float ny = Mathf.PerlinNoise(_seedB + 5.1f, Time.time * 0.05f + _seedA) - 0.5f;
        Vector2 heading = new Vector2(nx, ny * 0.55f);
        if (heading.sqrMagnitude < 1e-4f) heading = new Vector2(_facing, 0f);
        else heading.Normalize();

        // Subtle swimming yaw — gives a "living" feel without flicker.
        _wobblePhase += dt * Mathf.Lerp(1.4f, 2.6f, t01);
        heading = Rotate(heading, Mathf.Sin(_wobblePhase) * 4f * Mathf.Deg2Rad);

        float targetSpeed = speed * Mathf.Lerp(0.85f, 1.15f, t01);
        BlendVelocityToward(heading * targetSpeed + accel * speed * dt, 4.5f);
    }

    /// <summary>1 — Wiggler: rhythmic strong side-to-side body wave while moving forward.</summary>
    void ApplyWiggler(float speed, float t01, Vector2 accel, float dt)
    {
        if (_velocity.sqrMagnitude < 1e-4f)
            _velocity = new Vector2(_facing * speed * 0.6f, 0f);

        // Forward direction sticks to facing so the wave doesn't randomly reverse.
        float ny = Mathf.PerlinNoise(_seedA + 2.3f, Time.time * 0.10f) - 0.5f;
        Vector2 forward = new Vector2(_facing, ny * 0.55f);
        if (forward.sqrMagnitude < 1e-4f) forward = new Vector2(_facing, 0f);
        else forward.Normalize();

        // Big rhythmic perpendicular wave — this is the visual signature.
        float freq = Mathf.Lerp(2.4f, 4.6f, t01);
        float amplitude = Mathf.Lerp(0.55f, 1.05f, t01) * speed;
        Vector2 side = new Vector2(-forward.y, forward.x);
        Vector2 wave = side * Mathf.Sin(Time.time * freq * Mathf.PI * 2f + _seedA * 7.3f) * amplitude;

        float forwardSpeed = speed * Mathf.Lerp(0.85f, 1.15f, t01);
        BlendVelocityToward(forward * forwardSpeed + wave + accel * speed * dt * 0.6f, 6.5f);
    }

    /// <summary>2 — Darter: sits twitching, then bursts in a quick direction, then sits again.</summary>
    void ApplyDarter(float speed, float t01, Vector2 accel, float dt)
    {
        if (_darterIsDashing)
        {
            _darterDashTimer -= dt;
            BlendVelocityToward(_darterDashDir * speed * 4.2f, 14f);
            if (_darterDashTimer <= 0f)
            {
                _darterIsDashing = false;
                _darterIdleTimer = RandRange(Mathf.Lerp(1.2f, 0.5f, t01), Mathf.Lerp(2.6f, 1.2f, t01));
                _darterTwitchVel = Vector2.zero;
            }
            return;
        }

        // Hover-pause with periodic small twitches (head shakes / fin flicks).
        _darterIdleTimer -= dt;
        _darterTwitchTimer -= dt;
        if (_darterTwitchTimer <= 0f)
        {
            float ang = RandRange(-Mathf.PI, Mathf.PI);
            _darterTwitchVel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f) * speed * 0.45f;
            _darterTwitchTimer = RandRange(Mathf.Lerp(0.55f, 0.20f, t01), Mathf.Lerp(1.1f, 0.45f, t01));
        }
        // Twitch decays quickly so the fish actually pauses between shakes.
        _darterTwitchVel *= Mathf.Exp(-4f * dt);
        BlendVelocityToward(_darterTwitchVel + accel * speed * dt * 0.4f, 6f);

        if (_darterIdleTimer <= 0f)
        {
            _darterIsDashing = true;
            _darterDashTimer = RandRange(0.22f, 0.42f);
            float ang = RandRange(-Mathf.PI, Mathf.PI);
            // Bias dash direction away from current edges so dashes feel intentional, not chaotic.
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.7f);
            if (accel.sqrMagnitude > 0.01f) dir += accel.normalized * 0.6f;
            _darterDashDir = dir.normalized;
        }
    }

    /// <summary>3 — Patroller: marches between two endpoints, banking turns, brief pauses.</summary>
    void ApplyPatroller(float speed, float t01, Vector2 accel, float dt)
    {
        if (!_patrolInit)
        {
            _patrolA = new Vector2(
                RandRange(-tankExtents.x * 0.85f, -tankExtents.x * 0.25f),
                RandRange(-tankExtents.y * 0.55f, tankExtents.y * 0.55f));
            _patrolB = new Vector2(
                RandRange(tankExtents.x * 0.25f, tankExtents.x * 0.85f),
                RandRange(-tankExtents.y * 0.55f, tankExtents.y * 0.55f));
            _patrolToB = 1;
            _patrolInit = true;
        }

        Vector2 here = transform.position;
        Vector2 target = _patrolToB == 1 ? _patrolB : _patrolA;
        Vector2 toTarget = target - here;
        float dist = toTarget.magnitude;

        if (_patrolPauseTimer > 0f)
        {
            _patrolPauseTimer -= dt;
            BlendVelocityToward(accel * speed * dt * 0.4f, 5f);
            return;
        }

        if (dist < 0.45f)
        {
            _patrolToB = 1 - _patrolToB;
            _patrolPauseTimer = RandRange(Mathf.Lerp(0.7f, 0.2f, t01), Mathf.Lerp(1.4f, 0.55f, t01));
            // Re-roll the endpoint we just arrived at so the patrol path slowly evolves.
            if (_patrolToB == 0)
                _patrolB = new Vector2(
                    RandRange(tankExtents.x * 0.25f, tankExtents.x * 0.85f),
                    RandRange(-tankExtents.y * 0.55f, tankExtents.y * 0.55f));
            else
                _patrolA = new Vector2(
                    RandRange(-tankExtents.x * 0.85f, -tankExtents.x * 0.25f),
                    RandRange(-tankExtents.y * 0.55f, tankExtents.y * 0.55f));
            return;
        }

        Vector2 dir = toTarget / dist;
        // Slow down on approach so the turn-around banks naturally.
        float tspeed = speed * Mathf.Lerp(0.55f, 1.15f, Mathf.Clamp01(dist * 0.5f));

        _wobblePhase += dt * Mathf.Lerp(1.6f, 2.6f, t01);
        dir = Rotate(dir, Mathf.Sin(_wobblePhase) * 3.5f * Mathf.Deg2Rad);

        BlendVelocityToward(dir * tspeed + accel * speed * dt * 0.5f, 5f);
    }

    /// <summary>4 — Acrobat: travels horizontally, then performs a full barrel-roll loop, then continues.</summary>
    void ApplyAcrobat(float speed, float t01, Vector2 accel, float dt)
    {
        if (_acroPhase == 0)
        {
            _acroTravelTimer -= dt;

            float ny = Mathf.PerlinNoise(_seedA + 3.7f, Time.time * 0.08f) - 0.5f;
            Vector2 forward = new Vector2(_facing, ny * 0.45f);
            if (forward.sqrMagnitude < 1e-4f) forward = new Vector2(_facing, 0f);
            else forward.Normalize();

            // Subtle yaw wobble during travel for life.
            _wobblePhase += dt * Mathf.Lerp(1.8f, 3.0f, t01);
            forward = Rotate(forward, Mathf.Sin(_wobblePhase) * 4f * Mathf.Deg2Rad);

            float forwardSpeed = speed * Mathf.Lerp(0.95f, 1.25f, t01);
            BlendVelocityToward(forward * forwardSpeed + accel * speed * dt, 5f);

            if (_acroTravelTimer <= 0f)
            {
                _acroPhase = 1;
                _acroLoopAngle = 0f;
                _acroLoopFwd = _velocity.sqrMagnitude > 1e-4f ? _velocity.normalized : new Vector2(_facing, 0f);
                _acroLoopSign = _rng.NextDouble() > 0.5 ? 1f : -1f;
                _acroLoopDuration = RandRange(Mathf.Lerp(1.05f, 0.65f, t01), Mathf.Lerp(1.55f, 0.95f, t01));
            }
            return;
        }

        // Loop phase: trace a full revolution in the (forward, perp) plane around the entry point.
        float omega = Mathf.PI * 2f / _acroLoopDuration;
        _acroLoopAngle += omega * dt;

        Vector2 fwd = _acroLoopFwd;
        Vector2 perp = new Vector2(-fwd.y, fwd.x) * _acroLoopSign;
        Vector2 tangent = -fwd * Mathf.Sin(_acroLoopAngle) + perp * Mathf.Cos(_acroLoopAngle);

        float loopSpeed = speed * Mathf.Lerp(1.7f, 2.1f, t01);
        BlendVelocityToward(tangent * loopSpeed + accel * speed * dt * 0.25f, 12f);

        if (_acroLoopAngle >= Mathf.PI * 2f - 0.05f)
        {
            _acroPhase = 0;
            _acroTravelTimer = RandRange(Mathf.Lerp(4.5f, 2.0f, t01), Mathf.Lerp(7.0f, 3.5f, t01));
        }
    }

    /// <summary>
    /// Frame-rate-independent velocity follow. <paramref name="strength"/> is in 1/seconds —
    /// e.g. 6 means roughly half the gap closes every ~0.12s.
    /// </summary>
    void BlendVelocityToward(Vector2 target, float strength = 6f)
    {
        float t = 1f - Mathf.Exp(-strength * Time.deltaTime);
        _velocity = Vector2.Lerp(_velocity, target, t);
    }

    static Vector2 Rotate(Vector2 v, float radians)
    {
        float cs = Mathf.Cos(radians);
        float sn = Mathf.Sin(radians);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    void UpdateFacingAndFlip()
    {
        // Low-pass filter on horizontal velocity so brief jitter or perpendicular bobs don't
        // flip the sprite. Facing only flips when the fish has been moving sideways for a while.
        float dt = Time.deltaTime;
        float blend = 1f - Mathf.Exp(-2.5f * dt);
        _facingMomentum = Mathf.Lerp(_facingMomentum, _velocity.x, blend);

        const float thresh = 0.18f;
        if (_facingMomentum > thresh)
            _facing = 1f;
        else if (_facingMomentum < -thresh)
            _facing = -1f;

        Vector3 s = transform.localScale;
        float ax = Mathf.Abs(s.x);
        s.x = _facing * ax;
        transform.localScale = s;
    }

    void ClampToTankReflect()
    {
        Vector2 p = transform.position;
        Vector2 extent = tankExtents;

        Vector2 refl = Vector2.one;
        if (p.x > extent.x || p.x < -extent.x)
        {
            refl.x *= -1f;
            p.x = Mathf.Sign(p.x) * (extent.x - 0.01f);
        }

        if (p.y > extent.y || p.y < -extent.y)
        {
            refl.y *= -1f;
            p.y = Mathf.Sign(p.y) * (extent.y - 0.01f);
        }

        transform.position = p;

        _velocity.Scale(refl);
        if (refl.x < 0f || refl.y < 0f)
            _velocity *= Mathf.Lerp(0.75f, 0.94f, 0.85f + P("temperament") * 0.06f);
    }
}
