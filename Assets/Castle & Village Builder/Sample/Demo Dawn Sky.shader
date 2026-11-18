// The demo scene's sky: a dawn gradient (warm at the horizon, blue overhead) with a soft sun, for URP.
Shader "Castle & Village Builder/Demo Dawn Sky"
{
    Properties
    {
        _TopColour ("Overhead", Color) = (0.36, 0.5, 0.68, 1)
        _MidColour ("Above the horizon", Color) = (0.92, 0.8, 0.52, 1)
        _HorizonColour ("Horizon", Color) = (0.98, 0.62, 0.36, 1)
        _GroundColour ("Below the horizon", Color) = (0.78, 0.62, 0.55, 1)
        _SunColour ("Sun", Color) = (1, 0.85, 0.65, 1)
        _SunSize ("Sun size", Range(0.0001, 0.02)) = 0.0012
        _SunGlow ("Sun glow", Range(0, 4)) = 1.2
        _Exposure ("Exposure", Range(0, 4)) = 1.1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColour, _MidColour, _HorizonColour, _GroundColour, _SunColour;
                half _SunSize, _SunGlow, _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float y = d.y;
                half3 sky = y >= 0
                    ? lerp(lerp(_HorizonColour.rgb, _MidColour.rgb, smoothstep(0.0, 0.12, y)), _TopColour.rgb, smoothstep(0.03, 0.38, y))
                    : lerp(_HorizonColour.rgb, _GroundColour.rgb, smoothstep(0.0, 0.08, -y));

                // The sun (the scene's main light): a soft disc with a wide warm glow round it
                float3 toSun = _MainLightPosition.xyz;
                float c = saturate(dot(d, toSun));
                half disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, c);
                half glow = pow(c, 24.0) * 0.45 + pow(c, 4.0) * 0.18;
                sky += _SunColour.rgb * (disc * 3.0 + glow * _SunGlow) * step(-0.05, y);
                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
