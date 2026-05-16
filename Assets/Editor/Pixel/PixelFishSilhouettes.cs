using UnityEngine;

namespace Aquarium.PixelArt.EditorTools
{
    /// <summary>
    /// Parametric pixel silhouettes for every fish part. Each function takes
    /// canvas dimensions + variant/frame parameters and returns a delegate
    /// the rasterizer can call per pixel.
    ///
    /// The fish faces RIGHT in source art: head at +x (right side of canvas),
    /// tail attach at -x. <see cref="FishCompositor"/> mirrors via
    /// <see cref="UnityEngine.SpriteRenderer.flipX"/> for left-facing fish.
    /// </summary>
    public static class PixelFishSilhouettes
    {
        // ---- BODY ----------------------------------------------------------

        /// <summary>Spine sway offset in pixels, per (t in 0..1, frame). Drives swim cycle.</summary>
        public static float SpineSway(float t, int frame, int totalFrames, float ampPx)
        {
            if (totalFrames <= 1) return 0f;
            float phase = frame * Mathf.PI * 2f / totalFrames;
            // Wave with phase, amplitude grows toward tail (t→0).
            float taperFromHead = 1f - t;
            return Mathf.Sin(t * Mathf.PI * 1.6f + phase) * ampPx * taperFromHead;
        }

        /// <summary>Horizontal body span inside the sprite, as normalized x start/end.</summary>
        public static Vector2 BodyXRange01(int shape)
        {
            switch (shape)
            {
                case 1: return new Vector2(0.14f, 0.88f); // round
                case 2: return new Vector2(0.12f, 0.90f); // small oval
                case 4: return new Vector2(0.18f, 0.84f); // tall cute
                case 6: return new Vector2(0.16f, 0.86f); // baby round
                default: return new Vector2(0.10f, 0.92f);
            }
        }

        /// <summary>Maximum half-height of the body at fraction t (0=tail, 1=head), in pixels.</summary>
        public static float BodyHalfHeight(int shape, float t, float baseHalf)
        {
            // Keep the body variants visually distinct while biasing them away
            // from harsh geometry. Fins/tails handle attachment; bodies should
            // preserve genetic variety.
            t = Mathf.Clamp01(t);
            float bell = Mathf.Sin(t * Mathf.PI);
            float softBell = Mathf.Pow(Mathf.Max(0f, bell), 0.45f);

            switch (shape)
            {
                case 0: // Classic pet fish oval
                    return baseHalf * (0.92f * softBell + 0.08f * Mathf.SmoothStep(0.25f, 0.85f, t));
                case 1: // Round puffer/goldfish
                    return baseHalf * 1.18f * Mathf.Pow(Mathf.Max(0f, bell), 0.34f);
                case 2: // Long slender
                    return baseHalf * 0.48f * Mathf.Pow(Mathf.Max(0f, bell), 0.85f);
                case 3: // Clownfish-like, fuller head
                    return baseHalf * softBell * Mathf.Lerp(0.78f, 1.10f, Mathf.SmoothStep(0.35f, 0.85f, t));
                case 4: // Tall rounded fancy fish
                    return baseHalf * 1.22f * Mathf.Pow(Mathf.Max(0f, bell), 0.55f);
                case 5: // Chubby head goldfish
                    return baseHalf * softBell * Mathf.Lerp(0.82f, 1.22f, Mathf.SmoothStep(0.48f, 0.92f, t));
                case 6: // Ribbon / eel-like
                    return baseHalf * 0.30f * Mathf.Pow(Mathf.Max(0f, bell), 0.70f);
                case 7: // Soft diamond, not too sharp
                    return baseHalf * 0.90f * Mathf.Pow(Mathf.Max(0f, bell), 1.15f);
                default:
                    return baseHalf * 0.9f * softBell;
            }
        }

        /// <summary>Vertical spine offset (asymmetry) for shapes that aren't horizontally symmetric.</summary>
        public static float BodySpineBias(int shape, float t)
        {
            switch (shape)
            {
                case 3:
                    return Mathf.Lerp(0f, -1.2f, Mathf.SmoothStep(0.45f, 1f, t));
                case 5: // Chubby head: spine bows down toward head so big head reads.
                    return Mathf.Lerp(0f, -2.2f, Mathf.Pow(t, 2f));
                default:
                    return 0f;
            }
        }

        public static SilhouetteFn Body(int shape, int frame, int totalFrames, int canvasW, int canvasH)
        {
            // Reserve a 2-px margin so outline ring fits.
            int marginX = 4;
            int marginY = 4;
            float baseHalf = (canvasH - marginY * 2) * 0.5f;

            Vector2 bodyRange = BodyXRange01(shape);
            int minX = Mathf.RoundToInt(Mathf.Lerp(marginX, canvasW - 1 - marginX, bodyRange.x));
            int maxX = Mathf.RoundToInt(Mathf.Lerp(marginX, canvasW - 1 - marginX, bodyRange.y));
            float swayAmp = 1.05f;
            return (x, y) =>
            {
                if (x < minX || x > maxX) return false;

                float t = Mathf.InverseLerp(minX, maxX, x); // 0 at tail, 1 at head
                float halfH = BodyHalfHeight(shape, t, baseHalf);
                float spineY = canvasH * 0.5f + SpineSway(t, frame, totalFrames, swayAmp) + BodySpineBias(shape, t);
                return Mathf.Abs(y - spineY) <= halfH;
            };
        }

        // ---- TAIL ----------------------------------------------------------

        /// <summary>
        /// Tail silhouettes. Attach point is RIGHT-CENTER of the canvas
        /// (i.e. tail extends LEFTWARD from x = canvasW-1, y = canvasH/2).
        /// Variant 0..5 mirrors the existing tail_type gene values.
        /// </summary>
        public static SilhouetteFn Tail(int variant, int frame, int totalFrames, int canvasW, int canvasH)
        {
            float ax = canvasW - 1f;
            float ay = canvasH * 0.5f;
            // Per-frame wag: rotate the tail around the attach point by a soft angle.
            float wagAng = (totalFrames > 1
                ? Mathf.Sin(frame * Mathf.PI * 2f / totalFrames) * 9f * Mathf.Deg2Rad
                : 0f);
            float cs = Mathf.Cos(-wagAng);
            float sn = Mathf.Sin(-wagAng);

            return (x, y) =>
            {
                // Rotate (x,y) into "tail local" frame around attach point.
                float dx = ax - x;            // grows leftward, 0 at attach
                float dy = y - ay;            // 0 at attach mid-line
                float lx = dx * cs - dy * sn;
                float ly = dx * sn + dy * cs;
                if (lx < 0f) return false;
                return TailContainsPx(variant, lx, ly, canvasW, canvasH);
            };
        }

        static bool TailContainsPx(int variant, float dx, float dy, int W, int H)
        {
            switch (variant)
            {
                case 0: // Compact fan
                {
                    float maxX = W * 0.58f;
                    float halfHt = H * 0.06f + dx * 0.38f;
                    return dx >= 0f && dx <= maxX && Mathf.Abs(dy) <= halfHt;
                }
                case 1: // Rounded veil
                {
                    float maxX = W * 0.62f;
                    float halfHt = H * 0.08f + dx * 0.22f;
                    if (dx > maxX * 0.50f) halfHt += (dx - maxX * 0.50f) * 0.08f;
                    return dx >= 0f && dx <= maxX && Mathf.Abs(dy) <= halfHt;
                }
                case 2: // Webbed split fan
                {
                    float maxX = W * 0.60f;
                    float t = Mathf.Clamp01(dx / maxX);
                    float halfHt = H * (0.10f + 0.32f * t);
                    bool inFan = dx >= 0f && dx <= maxX && Mathf.Abs(dy) <= halfHt;
                    if (!inFan) return false;

                    // Suggest two lobes by shaping the outer edge, but keep the
                    // center web filled so it never reads as detached triangles.
                    if (t > 0.74f)
                    {
                        float centerWeb = Mathf.Lerp(H * 0.11f, H * 0.16f, Mathf.InverseLerp(0.74f, 1f, t));
                        if (Mathf.Abs(dy) <= centerWeb)
                            return true;
                    }
                    return true;
                }
                case 3: // Rounded paddle
                {
                    float r = H * 0.42f;
                    float cx = W * 0.24f;
                    float dist = Mathf.Sqrt((dx - cx) * (dx - cx) + dy * dy);
                    return dx >= 0f && dx <= W * 0.58f && dist <= r;
                }
                case 4: // Soft forked fan
                {
                    float maxX = W * 0.60f;
                    float halfHt = H * 0.06f + dx * 0.38f;
                    bool inFan = dx >= 0f && dx <= maxX && Mathf.Abs(dy) <= halfHt;
                    if (!inFan) return false;

                    // Keep a solid root and cut only a V-shaped notch from the
                    // free edge. The old logic removed bands inside the lobes,
                    // which read as holes.
                    if (dx < maxX * 0.55f)
                        return true;

                    float notchWidth = W * 0.30f;
                    if (dx > maxX - notchWidth)
                    {
                        float t = Mathf.InverseLerp(maxX - notchWidth, maxX, dx);
                        float notchHalf = Mathf.Lerp(0f, H * 0.16f, t);
                        if (Mathf.Abs(dy) <= notchHalf)
                            return false;
                    }
                    return true;
                }
                case 5: // Halfmoon, wide but stubby
                {
                    float r = H * 0.54f;
                    float cx = W * 0.20f;
                    float dist = Mathf.Sqrt((dx - cx) * (dx - cx) + dy * dy);
                    return dx >= 0f && dx <= W * 0.62f && dist <= r;
                }
            }
            return false;
        }

        // ---- PECTORAL FIN --------------------------------------------------

        /// <summary>
        /// Pectoral fin silhouettes. Attach point is TOP-CENTER of canvas
        /// (fin extends DOWNWARD from x=canvasW/2, y=0). Variant 0..4 mirrors
        /// fin_shape gene.
        /// </summary>
        public static SilhouetteFn PectoralFin(int variant, int frame, int totalFrames, int canvasW, int canvasH)
        {
            float ax = canvasW * 0.5f;
            float ay = 0f;
            // Flutter: small rotation around attach.
            float angle = (totalFrames > 1
                ? Mathf.Sin(frame * Mathf.PI * 2f / totalFrames) * 8f * Mathf.Deg2Rad
                : 0f);
            float cs = Mathf.Cos(angle);
            float sn = Mathf.Sin(angle);

            return (x, y) =>
            {
                float dx = x - ax;
                float dy = y - ay;
                float lx = dx * cs - dy * sn;
                float ly = dx * sn + dy * cs;
                if (ly < 0f) return false;
                return FinContainsPx(variant, lx, ly, canvasW, canvasH);
            };
        }

        public static SilhouetteFn DorsalFin(int variant, int frame, int totalFrames, int canvasW, int canvasH)
        {
            // Dorsal uses same shape pool but flipped vertically (attach at
            // bottom-center, points up). Compositor will rotate as needed but
            // we generate it pre-flipped so the part's pivot makes sense.
            float ax = canvasW * 0.5f;
            float ay = canvasH - 1f;
            float angle = (totalFrames > 1
                ? Mathf.Sin(frame * Mathf.PI * 2f / totalFrames) * 5f * Mathf.Deg2Rad
                : 0f);
            float cs = Mathf.Cos(angle);
            float sn = Mathf.Sin(angle);

            return (x, y) =>
            {
                float dx = x - ax;
                float dy = ay - y; // flip: grows upward
                float lx = dx * cs - dy * sn;
                float ly = dx * sn + dy * cs;
                if (ly < 0f) return false;
                return FinContainsPx(variant, lx, ly, canvasW, canvasH);
            };
        }

        static bool FinContainsPx(int variant, float dx, float dy, int W, int H)
        {
            float rootHalf = W * 0.13f;
            bool root = dy >= 0f && dy <= H * 0.18f && Mathf.Abs(dx) <= rootHalf;

            switch (variant)
            {
                case 0: // Rounded paddle
                {
                    float rx = W * 0.24f;
                    float ry = H * 0.34f;
                    float ndx = dx / rx;
                    float ndy = (dy - H * 0.30f) / ry;
                    return root || (ndx * ndx + ndy * ndy <= 1f && dy <= H * 0.66f);
                }
                case 1: // Teardrop side fin
                {
                    float maxY = H * 0.58f;
                    float t = Mathf.Clamp01(dy / maxY);
                    float halfWd = W * (0.12f + 0.18f * Mathf.Sin(t * Mathf.PI));
                    halfWd *= 1f - 0.35f * t;
                    return root || (dy >= 0f && dy <= maxY && Mathf.Abs(dx) <= halfWd);
                }
                case 2: // Short triangular fin
                {
                    float maxY = H * 0.72f;
                    float t = Mathf.Clamp01(dy / maxY);
                    float halfWd = W * Mathf.Lerp(0.28f, 0.06f, t);
                    return root || (dy >= 0f && dy <= maxY && Mathf.Abs(dx) <= halfWd);
                }
                case 3: // Soft sail fin
                {
                    float maxY = H * 0.62f;
                    float t = Mathf.Clamp01(dy / maxY);
                    float halfWd = W * (0.16f + 0.16f * Mathf.Sin(t * Mathf.PI * 0.9f));
                    if (t > 0.72f)
                        halfWd *= Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(0.72f, 1f, t));
                    return root || (dy >= 0f && dy <= maxY && Mathf.Abs(dx) <= halfWd);
                }
                case 4: // Small rounded nub fin
                {
                    float rx = W * 0.18f;
                    float ry = H * 0.24f;
                    float ndx = dx / rx;
                    float ndy = (dy - H * 0.24f) / ry;
                    return root || (dy >= 0f && dy <= H * 0.48f && ndx * ndx + ndy * ndy <= 1f);
                }
            }
            return false;
        }

        // ---- EYE -----------------------------------------------------------

        /// <summary>
        /// Renders a stylised pixel eye into the canvas directly (writes both
        /// shape + colour-tier encoding, no separate raster pass).
        /// Frame 0 is open; subsequent frames close vertically (blink).
        /// </summary>
        public static void DrawEye(PixelCanvas canvas, int sizeTier, int frame, int totalFrames, int sizeTierCount)
        {
            int W = canvas.width, H = canvas.height;
            float cx = (W - 1) * 0.5f;
            float cy = (H - 1) * 0.50f;

            // Larger sizeTier -> bigger eye.
            float tierT = sizeTierCount <= 1 ? 0.5f : sizeTier / (float)(sizeTierCount - 1);
            float eyeR = Mathf.Lerp(Mathf.Min(W, H) * 0.34f, Mathf.Min(W, H) * 0.47f, tierT);
            float pupilR = eyeR * 0.36f;
            float highlightR = Mathf.Max(1.1f, eyeR * 0.17f);
            Vector2 pupilOffset = new Vector2(eyeR * 0.13f, -eyeR * 0.05f);
            Vector2 highlightOffset = new Vector2(-eyeR * 0.22f, eyeR * 0.22f);

            // Blink: collapse vertical extent over animation frames.
            float blinkT = totalFrames <= 1 ? 0f : frame / (float)(totalFrames - 1);
            // First frame open (0), last frame fully closed (1). Cosine eases the close.
            float openY = Mathf.Lerp(1f, 0.05f, blinkT);

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = x - cx;
                float dy = (y - cy) / Mathf.Max(0.05f, openY); // squish for blink
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > eyeR + 1f)
                {
                    canvas.SetEncoded(x, y, 0f, 0f, 0f, 0f);
                    continue;
                }

                if (d > eyeR)
                {
                    // Outer outline ring.
                    canvas.SetEncoded(x, y, 0f, 0f, 0f, 1f);
                    continue;
                }

                float pupilD = Vector2.Distance(new Vector2(dx, dy), pupilOffset);
                float highlightD = Vector2.Distance(new Vector2(dx, dy), highlightOffset);

                if (highlightD <= highlightR)
                {
                    // Sparkle pixel cluster.
                    canvas.SetEncoded(x, y, 1f, 0f, 1f, 1f);
                    continue;
                }

                if (pupilD <= pupilR)
                {
                    // Pupil: outline-tier (palette outline colour).
                    canvas.SetEncoded(x, y, 0f, 0f, 0f, 1f);
                    continue;
                }

                // Eye white: bright encoded body pixels so it stays readable.
                float shade = PixelArtRaster.QuantizeTiers(Mathf.Lerp(0.95f, 0.75f, d / eyeR), 3);
                canvas.SetEncoded(x, y, shade, 0f, 1f, 1f);
            }
        }

        // ---- MOUTH ---------------------------------------------------------

        /// <summary>
        /// Mouth/lip variants. Attach at LEFT-CENTER (mouth extends rightward
        /// from snout when fish faces right). Variant 0..3 = neutral/grin/pout/beak.
        /// </summary>
        public static SilhouetteFn Mouth(int variant, int canvasW, int canvasH)
        {
            // Mouth sprites pivot on the left edge; draw visible pixels next to
            // that pivot so the compositor anchor actually lands on the snout.
            float cx = 2f;
            float cy = canvasH * 0.5f;
            switch (variant)
            {
                case 0: // Neutral - small flat line
                    return (x, y) =>
                        x >= cx && x <= cx + canvasW * 0.32f && Mathf.Abs(y - cy) <= 1f;
                case 1: // Grin - slight upward curve
                    return (x, y) =>
                    {
                        float len = canvasW * 0.42f;
                        float t = Mathf.InverseLerp(cx, cx + len, x);
                        if (t < 0f || t > 1f) return false;
                        float curve = -Mathf.Sin(t * Mathf.PI) * 1.8f;
                        return Mathf.Abs(y - cy - curve) <= 1.2f;
                    };
                case 2: // Pout - tiny round mouth
                    return (x, y) =>
                    {
                        float dx = x - (cx + 2f);
                        float dy = y - cy;
                        return dx * dx + dy * dy <= 3.5f;
                    };
                case 3: // Beak - soft kissy protrusion
                    return (x, y) =>
                    {
                        float dx = x - cx;
                        float dy = y - cy;
                        return dx >= 0f && dx <= canvasW * 0.34f && Mathf.Abs(dy) <= (canvasW * 0.34f - dx) * 0.26f;
                    };
            }
            return (x, y) => false;
        }
    }
}
