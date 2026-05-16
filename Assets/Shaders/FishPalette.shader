Shader "Aquarium/FishPalette"
{
    // Pixel-art palette swap shader for fish parts.
    //
    // The sprite texture encodes shading + body mask in a packed RGBA:
    //   R = brightness tier (0 = darkest body shadow, 1 = brightest highlight)
    //   G = pattern mask    (0 = no pattern, 1 = full pattern colour)
    //   B = body interior   (0 = silhouette outline, 1 = body interior)
    //   A = silhouette alpha
    //
    // At runtime each fish injects its own palette via MaterialPropertyBlock,
    // giving us hundreds of unique looks from a single shared material without
    // breaking SRP batching.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.35, 0.6, 0.95, 1)
        _PatternColor ("Pattern Color", Color) = (0.95, 0.85, 0.4, 1)
        _OutlineColor ("Outline Color", Color) = (0.07, 0.05, 0.10, 1)
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _ShadowMul ("Shadow Multiplier", Range(0.2, 1.0)) = 0.55
        _HighlightMul ("Highlight Multiplier", Range(1.0, 2.0)) = 1.18
        _PatternStrength ("Pattern Strength", Range(0, 1)) = 1
        _PatternType ("Pattern Type (0..8)", Float) = 0
        _PatternScale ("Pattern Scale", Float) = 4
        _PatternContrast ("Pattern Contrast", Range(0, 1)) = 0.6
        _Iridescence ("Iridescence", Range(0, 1)) = 0
        _Bioluminescence ("Bioluminescence", Range(0, 1)) = 0
        _Transparency ("Transparency", Range(0, 1)) = 0
        _IridescencePhase ("Iridescence Phase", Float) = 0

        [Toggle(_UNLIT_TINT_ONLY)] _UnlitTintOnly ("Skip palette (use as plain sprite tint)", Float) = 0

        // Standard sprite blend props so it works in URP 2D.
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 color      : COLOR;
            float2 uv         : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 color      : COLOR;
            float2 uv         : TEXCOORD0;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseColor;
            float4 _PatternColor;
            float4 _OutlineColor;
            float4 _HighlightColor;
            float _ShadowMul;
            float _HighlightMul;
            float _PatternStrength;
            float _PatternType;
            float _PatternScale;
            float _PatternContrast;
            float _Iridescence;
            float _Bioluminescence;
            float _Transparency;
            float _IridescencePhase;
            float4 _Color;
        CBUFFER_END

        Varyings vert(Attributes v)
        {
            Varyings o;
            VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = p.positionCS;
            o.color = v.color * _Color;
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            return o;
        }

        // ------------------------------------------------------------------
        // Procedural patterns. Generated in shader (instead of baking per-body
        // x per-pattern sprites) and quantised to the sprite's pixel grid so
        // they read as pixel art rather than smooth math noise. Pattern_type
        // matches the gene's discrete states: 0 solid, 1 stripes, 2 spots,
        // 3 marble, 4 gradient, 5 iridescent, 6 outlined, 7 reticulated, 8 banded.
        // ------------------------------------------------------------------
        float Hash21(float2 p)
        {
            return frac(sin(dot(p, float2(127.103, 311.967))) * 43758.7123);
        }

        // Quantise a 0..1 uv to integer pixel cells of the body sprite so
        // patterns step cell-by-cell instead of smoothly fading.
        float2 SnapUV(float2 uv, float scale)
        {
            // Convert uv → cells, floor, then read at cell centre. scale acts
            // as "pixels per cell" so larger scale = chunkier pattern.
            float cells = max(scale, 1.0) * 7.0;
            return (floor(uv * cells) + 0.5) / cells;
        }

        float Stripes(float2 uv, float scale)
        {
            float v = sin((uv.x * 0.35 + uv.y) * scale * 5.2);
            return step(0.0, v);
        }

        float Spots(float2 uv, float scale)
        {
            float2 g = uv * scale * 1.05;
            float2 id = floor(g);
            float2 f = frac(g) - 0.5;
            float jitter = Hash21(id) * 0.25;
            float r = 0.26 + jitter * 0.08;
            float d = length(f);
            return step(d, r);
        }

        float Marble(float2 uv, float scale)
        {
            float2 suv = uv * scale;
            float n = Hash21(floor(suv * 4.0));
            float wave = sin((uv.x + uv.y * 1.6 + n * 0.5) * scale * 3.14);
            return step(0.0, wave);
        }

        float GradientPat(float2 uv) { return step(0.5, uv.x); }

        float Iridescent(float2 uv, float scale)
        {
            float v = sin((uv.x + uv.y * 0.7) * scale * 6.2831 + sin(uv.y * scale * 3.14));
            return step(0.0, v);
        }

        float Outlined(float2 uv, float scale)
        {
            float2 c = abs(uv - 0.5) * 2.0;
            float edge = max(c.x, c.y);
            return step(0.7, edge);
        }

        float Reticulated(float2 uv, float scale) { return 1.0 - Spots(uv, scale); }

        float Banded(float2 uv, float scale)
        {
            float v = sin(uv.x * scale * 9.0);
            return step(0.0, v);
        }

        float ProceduralPattern(float2 uv, int type, float scale, float contrast)
        {
            if (type <= 0 || contrast <= 0.001) return 0.0;
            float2 q = SnapUV(uv, scale);
            float p = 0.0;
            if (type == 1) p = Stripes(q, scale);
            else if (type == 2) p = Spots(q, scale);
            else if (type == 3) p = Marble(q, scale);
            else if (type == 4) p = GradientPat(q);
            else if (type == 5) p = Iridescent(q, scale);
            else if (type == 6) p = Outlined(q, scale);
            else if (type == 7) p = Reticulated(q, scale);
            else if (type == 8) p = Banded(q, scale);
            return p * saturate(contrast);
        }

        // HSV utilities so iridescence can rotate hue cleanly without the
        // awful rainbow-clip we'd get from RGB phase shifts.
        float3 RgbToHsv(float3 c)
        {
            float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            float e = 1.0e-10;
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
        }
        float3 HsvToRgb(float3 c)
        {
            float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
            float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
            return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
        }

        float4 frag(Varyings i) : SV_Target
        {
            float4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

            // Used for plain sprites (eye, glow) that don't follow the encoding scheme.
            #ifdef _UNLIT_TINT_ONLY
                float3 plain = src.rgb * _BaseColor.rgb * i.color.rgb;
                float plainA = src.a * i.color.a * (1.0 - saturate(_Transparency) * 0.85);
                return float4(plain, plainA);
            #endif

            // Decode encoded sprite channels.
            float shade = src.r;
            float interior = src.b;
            float silhouette = src.a;

            // Patterns: max of artist-encoded mask (G channel) and procedural
            // type generated from the gene. Gated by interior so patterns
            // never bleed onto the silhouette ring.
            int pType = (int)floor(_PatternType + 0.5);
            float proc = ProceduralPattern(i.uv, pType, max(_PatternScale, 0.5), _PatternContrast);
            float patternMask = saturate(max(src.g, proc) * saturate(_PatternStrength) * interior);

            // Pixel-art tier mapping: shadow .. base .. highlight.
            // shade<0.5 → lerp(shadow, base); shade>=0.5 → lerp(base, highlight).
            float3 baseRgb = _BaseColor.rgb;
            float3 patternRgb = _PatternColor.rgb;

            float highT = saturate((shade - 0.5) * 2.0);
            float3 baseHighlight = lerp(baseRgb * _HighlightMul, _HighlightColor.rgb, highT * 0.65);
            float3 patternHighlight = lerp(patternRgb * _HighlightMul, _HighlightColor.rgb, highT * 0.35);
            float3 baseShaded = lerp(baseRgb * _ShadowMul,
                                     lerp(baseRgb, baseHighlight, highT),
                                     step(0.5, shade));
            float3 patShaded = lerp(patternRgb * _ShadowMul,
                                    lerp(patternRgb, patternHighlight, highT),
                                    step(0.5, shade));

            float3 col = lerp(baseShaded, patShaded, patternMask);

            // Outline ring: where interior == 0 inside the silhouette, force outline color.
            // Sprites with no encoded interior (legacy / overlay sprites) have interior==0
            // even on the inside, so only treat as outline if alpha is also fully opaque
            // *and* shade is high (filled body uses interior>0 already).
            float outlineMask = (1.0 - interior) * step(0.5, silhouette);
            col = lerp(col, _OutlineColor.rgb, outlineMask);

            // Iridescence: rotate hue slightly with time + per-fish phase, gated by interior
            // so the outline stays inky.
            if (_Iridescence > 0.001)
            {
                float3 hsv = RgbToHsv(col);
                float t = _Time.y * 0.6 + _IridescencePhase;
                hsv.x = frac(hsv.x + sin(t) * 0.10 * _Iridescence);
                hsv.y = saturate(hsv.y + sin(t * 1.3) * 0.08 * _Iridescence);
                col = lerp(col, HsvToRgb(hsv), interior * _Iridescence);
            }

            // Bioluminescence: add additive emission tinted by base color, pulsed slowly.
            // Pulse only inside the body (not the outline).
            float pulse = sin(_Time.y * 1.8 + _IridescencePhase) * 0.2 + 0.8;
            col += baseRgb * _Bioluminescence * pulse * 0.55 * interior;

            // Transparency gene fades alpha rather than blending so dark fish
            // don't read as ghost-white.
            float alpha = silhouette * i.color.a * (1.0 - saturate(_Transparency) * 0.85);

            return float4(col * i.color.rgb, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "FishPalette2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _UNLIT_TINT_ONLY
            ENDHLSL
        }

        Pass
        {
            Name "FishPaletteForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _UNLIT_TINT_ONLY
            ENDHLSL
        }

        Pass
        {
            Name "FishPaletteSrpDefault"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _UNLIT_TINT_ONLY
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
