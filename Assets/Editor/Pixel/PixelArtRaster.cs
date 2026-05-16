using UnityEngine;

namespace Aquarium.PixelArt.EditorTools
{
    /// <summary>
    /// Pixel-grid rasterization helpers for the procedural fish-part generator.
    /// Output pixels follow the FishPalette shader's encoding scheme:
    ///   R = brightness tier  (0..1, 0=darkest body shadow, 1=brightest highlight)
    ///   G = pattern mask     (0 here; patterns are generated in shader)
    ///   B = body interior    (0=outline pixel, 1=interior pixel)
    ///   A = silhouette alpha (0 outside, 1 inside)
    /// </summary>
    public class PixelCanvas
    {
        public readonly int width;
        public readonly int height;
        public readonly Color[] pixels;

        public PixelCanvas(int w, int h)
        {
            width = w;
            height = h;
            pixels = new Color[w * h];
            // Default: fully transparent, all-zero (so unwritten pixels read as outside-silhouette).
        }

        public bool InBounds(int x, int y) => (uint)x < (uint)width && (uint)y < (uint)height;

        public void SetEncoded(int x, int y, float shadeR, float patternG, float interiorB, float alphaA)
        {
            if (!InBounds(x, y))
                return;
            pixels[y * width + x] = new Color(
                Mathf.Clamp01(shadeR),
                Mathf.Clamp01(patternG),
                Mathf.Clamp01(interiorB),
                Mathf.Clamp01(alphaA));
        }

        public Color Get(int x, int y) => InBounds(x, y) ? pixels[y * width + x] : default;

        public void Clear()
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = default;
        }
    }

    /// <summary>
    /// Functional silhouette: returns true if a pixel (x,y) is inside the part.
    /// Coordinates are pixel-space relative to the part's local origin.
    /// </summary>
    public delegate bool SilhouetteFn(int x, int y);

    public static class PixelArtRaster
    {
        /// <summary>
        /// Encode a body-style part: filled silhouette with countershading,
        /// inner shading, and a 1-2 px outline ring on the silhouette edge.
        /// Belly (low y) → highlight tier; dorsal (high y) → shadow tier.
        /// </summary>
        public static void RasterizeBody(
            PixelCanvas canvas,
            SilhouetteFn inside,
            int outlineThicknessPx = 2,
            float bellyHighlightBoost = 0.15f)
        {
            // Two-pass: first mark interior, then determine which interior
            // pixels are within outlineThickness px of the silhouette edge
            // and demote them to outline pixels.
            int w = canvas.width, h = canvas.height;

            // Compute per-pixel distance-to-silhouette-edge using a cheap
            // chamfer pass (sufficient at our resolutions; not fully Euclidean).
            int[] dist = new int[w * h];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = inside(i % w, i / w) ? int.MaxValue : 0;

            // Forward pass.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (dist[idx] == 0) continue;
                int best = dist[idx];
                if (x > 0)        best = Mathf.Min(best, dist[idx - 1] + 1);
                if (y > 0)        best = Mathf.Min(best, dist[idx - w] + 1);
                if (x > 0 && y > 0) best = Mathf.Min(best, dist[idx - w - 1] + 1);
                dist[idx] = best;
            }
            // Backward pass.
            for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int idx = y * w + x;
                if (dist[idx] == 0) continue;
                int best = dist[idx];
                if (x < w - 1)        best = Mathf.Min(best, dist[idx + 1] + 1);
                if (y < h - 1)        best = Mathf.Min(best, dist[idx + w] + 1);
                if (x < w - 1 && y < h - 1) best = Mathf.Min(best, dist[idx + w + 1] + 1);
                dist[idx] = best;
            }

            // Find bounds for normalised body shading.
            int minY = int.MaxValue, maxY = int.MinValue;
            for (int i = 0; i < dist.Length; i++)
            {
                if (dist[i] == 0) continue;
                int yy = i / w;
                if (yy < minY) minY = yy;
                if (yy > maxY) maxY = yy;
            }

            float ySpan = Mathf.Max(1, maxY - minY);

            // Encode pixels.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                int d = dist[idx];
                if (d == 0)
                {
                    canvas.pixels[idx] = default; // outside silhouette
                    continue;
                }

                // Outline ring → interior=0, shade=0 so palette shader uses outline colour.
                if (d <= outlineThicknessPx)
                {
                    canvas.pixels[idx] = new Color(0f, 0f, 0f, 1f);
                    continue;
                }

                // Countershading: soft bright belly, darker dorsal ridge.
                float yt = Mathf.InverseLerp(minY, maxY, y); // 0 at bottom (belly), 1 at top (dorsal)
                float bodyShade = Mathf.Lerp(0.92f + bellyHighlightBoost, 0.42f, yt);

                // A small front/top sparkle patch gives round fish a cute arcade highlight.
                float xt = x / Mathf.Max(1f, w - 1f);
                float highlightX = Mathf.InverseLerp(0.54f, 0.86f, xt) * (1f - Mathf.InverseLerp(0.86f, 0.98f, xt));
                float highlightY = Mathf.InverseLerp(0.46f, 0.18f, yt) * (1f - Mathf.InverseLerp(0.18f, 0.04f, yt));
                bodyShade += Mathf.Clamp01(highlightX * highlightY) * 0.22f;

                // Edge-darken (depth effect).
                float edgeT = Mathf.InverseLerp(outlineThicknessPx, outlineThicknessPx + 5f, d);
                bodyShade *= Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(edgeT));

                // Quantize to tiers for true pixel-art feel.
                bodyShade = QuantizeTiers(bodyShade, 4);

                canvas.pixels[idx] = new Color(bodyShade, 0f, 1f, 1f);
            }
        }

        /// <summary>
        /// Encode a fin/tail-style overlay: thin silhouette, gradient shading
        /// from attach edge (light) → free edge (dark), no inner outline ring
        /// because fins look better as solid shape with subtle banding.
        /// </summary>
        /// <param name="attachEdge">Direction (in pixel space) where the fin
        /// connects to the body — used to gradient shade from attach (1.0) to
        /// the opposite tip (0.55).</param>
        public static void RasterizeFinOrTail(
            PixelCanvas canvas,
            SilhouetteFn inside,
            Vector2 attachEdge,
            int outlineThicknessPx = 1,
            int rayBandPx = 5)
        {
            int w = canvas.width, h = canvas.height;

            // Distance to silhouette edge (same chamfer trick as body).
            int[] dist = new int[w * h];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = inside(i % w, i / w) ? int.MaxValue : 0;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (dist[idx] == 0) continue;
                int best = dist[idx];
                if (x > 0) best = Mathf.Min(best, dist[idx - 1] + 1);
                if (y > 0) best = Mathf.Min(best, dist[idx - w] + 1);
                if (x > 0 && y > 0) best = Mathf.Min(best, dist[idx - w - 1] + 1);
                dist[idx] = best;
            }
            for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int idx = y * w + x;
                if (dist[idx] == 0) continue;
                int best = dist[idx];
                if (x < w - 1) best = Mathf.Min(best, dist[idx + 1] + 1);
                if (y < h - 1) best = Mathf.Min(best, dist[idx + w] + 1);
                if (x < w - 1 && y < h - 1) best = Mathf.Min(best, dist[idx + w + 1] + 1);
                dist[idx] = best;
            }

            // Find silhouette bounds along attach direction so we can normalise
            // the attach→tip gradient to 0..1.
            float minProj = float.MaxValue, maxProj = float.MinValue;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (dist[y * w + x] == 0) continue;
                float p = x * attachEdge.x + y * attachEdge.y;
                if (p < minProj) minProj = p;
                if (p > maxProj) maxProj = p;
            }
            float projSpan = Mathf.Max(1f, maxProj - minProj);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                int d = dist[idx];
                if (d == 0)
                {
                    canvas.pixels[idx] = default;
                    continue;
                }

                if (d <= outlineThicknessPx)
                {
                    canvas.pixels[idx] = new Color(0f, 0f, 0f, 1f);
                    continue;
                }

                // Project pixel onto attach-edge direction. 1 = at attach
                // (where fin meets body, full brightness); 0 = at fin tip.
                float p = (x * attachEdge.x + y * attachEdge.y - minProj) / projSpan;
                float attachT = Mathf.Clamp01(1f - p); // 1 at attach, 0 at tip
                float baseShade = Mathf.Lerp(0.52f, 0.98f, attachT);

                // Optional rays: every rayBandPx pixels perpendicular to the
                // attach direction add hand-pixeled fin stripes.
                if (rayBandPx > 1)
                {
                    Vector2 perp = new Vector2(-attachEdge.y, attachEdge.x);
                    float along = x * perp.x + y * perp.y;
                    int band = Mathf.Abs(Mathf.RoundToInt(along)) % rayBandPx;
                    if (band == 0 || band == rayBandPx - 1)
                        baseShade *= 0.78f;
                }

                // Tiny terminal highlight at the attach edge keeps fins from reading flat.
                if (attachT > 0.82f && d > outlineThicknessPx + 1)
                    baseShade = Mathf.Max(baseShade, 0.95f);

                baseShade = QuantizeTiers(baseShade, 3);
                canvas.pixels[idx] = new Color(baseShade, 0f, 1f, 1f);
            }
        }

        /// <summary>
        /// Quantises a 0..1 value to the nearest of <paramref name="tierCount"/>
        /// evenly-spaced tiers and returns the tier as 0..1 again. Used to make
        /// continuous gradients read as pixel-art tiered shading.
        /// </summary>
        public static float QuantizeTiers(float v, int tierCount)
        {
            int t = Mathf.Clamp(Mathf.RoundToInt(v * (tierCount - 1)), 0, tierCount - 1);
            return tierCount <= 1 ? v : t / (float)(tierCount - 1);
        }
    }
}
