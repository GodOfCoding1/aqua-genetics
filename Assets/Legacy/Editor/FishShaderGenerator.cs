using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 4.1 — Writes <c>Assets/Shaders/FishPattern.shader</c> (hand-coded URP Unlit).
/// </summary>
public static class FishShaderGenerator
{
    const string AssetPath = "Assets/Shaders/FishPattern.shader";

    [MenuItem("Tools/Aquarium/Legacy/Generate Fish Pattern Shader")]
    public static void GenerateFishPatternShader()
    {
        EnsureAssetFolderExists("Assets/Shaders");

        string abs = Path.Combine(Application.dataPath, "Shaders/FishPattern.shader");
        File.WriteAllText(abs, ShaderSource, new System.Text.UTF8Encoding(false));
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log($"Fish pattern shader written to {AssetPath}");
    }

    static void EnsureAssetFolderExists(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        int lastSlash = assetPath.LastIndexOf('/');
        if (lastSlash <= 0)
            return;

        string parent = assetPath[..lastSlash];
        string folderName = assetPath[(lastSlash + 1)..];

        EnsureAssetFolderExists(parent);

        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    const string ShaderSource = @"Shader ""Aquarium/FishPattern""
{
    Properties
    {
        [MainColor] _BaseColor (""Base Color"", Color) = (0.35, 0.6, 0.95, 1)
        _PatternColor (""Pattern Color"", Color) = (0.95, 0.85, 0.4, 1)
        _PatternType (""Pattern Type"", Float) = 0
        _PatternScale (""Pattern Scale"", Float) = 4
        _PatternContrast (""Pattern Contrast"", Range(0, 1)) = 0.6
        _Iridescence (""Iridescence"", Range(0, 1)) = 0
        _Bioluminescence (""Bioluminescence"", Range(0, 1)) = 0
        _Transparency (""Transparency"", Range(0, 1)) = 0
    }

    SubShader
    {
        // URP 2D Renderer only renders passes whose LightMode is in its
        // tag set (Universal2D / NormalsRendering / SRPDefaultUnlit).
        // We provide one pass for the 2D renderer and one for the Forward
        // renderer so the shader works in either pipeline.
        Tags
        {
            ""RenderPipeline"" = ""UniversalPipeline""
            ""RenderType"" = ""Opaque""
            ""Queue"" = ""Geometry""
            ""UniversalMaterialType"" = ""Unlit""
            ""IgnoreProjector"" = ""True""
        }

        HLSLINCLUDE
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl""

        #ifndef UNITY_PI
        #define UNITY_PI 3.14159265359f
        #endif

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 uv2 : TEXCOORD1;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float2 uv2 : TEXCOORD3;
        };

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _PatternColor;
            float _PatternType;
            float _PatternScale;
            float _PatternContrast;
            float _Iridescence;
            float _Bioluminescence;
            float _Transparency;
        CBUFFER_END

        Varyings vert(Attributes v)
        {
            Varyings o;
            float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(positionWS);
            o.uv = v.uv;
            o.positionWS = positionWS;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv2 = v.uv2;
            return o;
        }

        float Hash21(float2 p)
        {
            return frac(sin(dot(p, float2(127.103, 311.967))) * 43758.7123);
        }

        float Stripes(float2 uv, float scale)
        {
            return sin(uv.y * scale * UNITY_PI * 2.0) * 0.5 + 0.5;
        }

        float Spots(float2 uv, float scale)
        {
            float2 g = uv * scale;
            float2 id = floor(g);
            float2 f = frac(g);
            float md = 1.0;

            UNITY_UNROLL
            for (int y = -1; y <= 1; y++)
            {
                UNITY_UNROLL
                for (int x = -1; x <= 1; x++)
                {
                    float2 nid = id + float2(float(x), float(y));
                    float2 jitter = float2(Hash21(nid), Hash21(nid + float2(19.71, 3.141))) - 0.5;
                    float2 p = float2(float(x), float(y)) - f + jitter * 0.62;
                    md = min(md, dot(p, p));
                }
            }

            return saturate(1.0 - md * 7.0);
        }

        float Marble(float2 uv, float scale)
        {
            float2 suv = uv * scale;
            float n = frac(sin(dot(suv, float2(128.943, 37.891))) * 39302.943);
            float n2 = frac(sin(dot(suv * 1.7 + n, float2(39.2, 11.7))) * 21341.1);
            float fbm = n * 0.65 + n2 * 0.35;
            return sin((uv.x + uv.y + fbm) * UNITY_PI * 2.0) * 0.5 + 0.5;
        }

        float GradientPat(float2 uv)
        {
            return saturate(uv.x);
        }

        float Outlined(float2 uv, float scale)
        {
            float2 c = abs(uv - 0.5) * 2.0;
            float edge = max(c.x, c.y);
            return abs(sin(edge * scale * UNITY_PI * 2.0)) * 0.5 + 0.5;
        }

        float Reticulated(float2 uv, float scale)
        {
            return 1.0 - Spots(uv, scale);
        }

        float Banded(float2 uv, float scale)
        {
            return abs(sin(uv.y * scale * UNITY_PI * 4.0)) * 0.5 + 0.5;
        }

        float IridescentPattern(float2 uv, float scale)
        {
            return sin(uv.x * scale * UNITY_PI * 2.0 + sin(uv.y * scale * UNITY_PI)) * 0.5 + 0.5;
        }

        float PatternValue(float2 uv, int type, float scale)
        {
            if (type == 0) return 1.0;
            if (type == 1) return Stripes(uv, scale);
            if (type == 2) return Spots(uv, scale);
            if (type == 3) return Marble(uv, scale);
            if (type == 4) return GradientPat(uv);
            if (type == 5) return IridescentPattern(uv, scale);
            if (type == 6) return Outlined(uv, scale);
            if (type == 7) return Reticulated(uv, scale);
            if (type == 8) return Banded(uv, scale);
            return 1.0;
        }

        float4 frag(Varyings i) : SV_Target
        {
            int pType = (int)floor(_PatternType + 0.5);
            pType = clamp(pType, 0, 8);
            float scale = max(_PatternScale, 0.01);

            float pat = PatternValue(i.uv, pType, scale);
            float blend = lerp(1.0, pat, saturate(_PatternContrast));
            float3 col = lerp(_BaseColor.rgb, _PatternColor.rgb, blend);

            // Countershading: belly (uv.y near 0) lighter, dorsal darker.
            float belly = 1.0 - i.uv.y;
            float shade = lerp(0.78, 1.18, smoothstep(0.0, 1.0, belly));
            col *= shade;

            // Inner-edge darkening from per-vertex boundary distance (uv2.x).
            float boundary = saturate(i.uv2.x);
            float edge = smoothstep(0.82, 1.0, boundary);
            col *= lerp(1.0, 0.65, edge);

            float3 n = normalize(i.normalWS);
            float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
            // Slow, subtle shimmer: amplitude halved, time slowed.
            float irid = sin(dot(v, n) * UNITY_PI + _Time.y * 0.5) * _Iridescence * 0.15;
            col += irid;

            // Slow, subtle pulse: amplitude/rate halved.
            float pulse = sin(_Time.y * 1.0) * 0.15 + 0.85;
            float3 emission = _BaseColor.rgb * _Bioluminescence * pulse * 0.9;
            col += emission;

            float t = saturate(_Transparency);
            col.rgb *= (1.0 - t * 0.85);

            return float4(col, 1.0);
        }
        ENDHLSL

        // Pass picked up by the URP 2D Renderer.
        Pass
        {
            Name ""FishPattern2D""
            Tags { ""LightMode"" = ""Universal2D"" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        // Pass picked up by the URP Forward renderer (kept so the shader
        // also renders if you ever switch to a 3D URP camera).
        Pass
        {
            Name ""FishPatternForward""
            Tags { ""LightMode"" = ""UniversalForward"" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    FallBack Off
}
";
}
