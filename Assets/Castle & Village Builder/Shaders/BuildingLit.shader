// URP's Lit, wet in the rain: for the Medieval Village kit's houses and the castle walls (Scatter Buildings). It's lit
// exactly as Lit is (the Day Night Cycle's sun, sky light, fog and reflections), and while the Weather Controller's
// _Wetness rises its stone, plaster, wood and tiles go darker and richer and take a glossy sheen (Wetness.hlsl), the
// tops of things (roofs, walkways, sills) most. Its other passes (shadows, depth) are Lit's own.
Shader "Custom/Building Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecColor("Moss Colour (alpha: moss in joints)", Color) = (0.27, 0.36, 0.14, 0.5)
        _SpecGlossMap("Specular", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Colour Variation", Range(0.0, 1.0)) = 0.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Moss Patch Size", Range(0.2, 4.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}
        // Weathering (Castle Walls > Look > Weathering): these use Lit's clear coat and detail slots, which this shader
        // doesn't otherwise use, so the material stays SRP Batcher friendly
        [Header(Weathering)]
        [Toggle(_WEATHERED)] _Weathered("Weathered", Float) = 0
        _ClearCoatMask("Grime and Streaks", Range(0.0, 1.0)) = 0.0
        _ClearCoatSmoothness("Moss On Tops", Range(0.0, 1.0)) = 0.0


        // Blending state (as Lit's, for its passes)
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment BuildingLitFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local_fragment _WEATHERED
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "BuildingWetness.hlsl"

            // Lit's fragment, with the surface wetted as it rains
            void BuildingLitFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(_PARALLAXMAP)
                // (depth: the texture shifted by its height map as you look across it, so mortar and cracks sink in)
            #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirTS = input.viewDirTS;
            #else
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
            #endif
                ApplyPerPixelDisplacement(viewDirTS, input.uv);
            #endif

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

            #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
            #endif

            #if defined(_WEATHERED)
                // Weathered stone: big soft patches of colour (so the tiling doesn't show), grime in the joints and
                // streaks run down the walls by the rain, and moss on the tops and creeping into the joints
                float3 pw = inputData.positionWS;
                half3 nw = inputData.normalWS;
                half upness = saturate(nw.y), wall = 1.0 - saturate(abs(nw.y) * 1.6);
                float3 alongWall = normalize(cross(float3(0, 1, 0), nw) + float3(0.0001, 0, 0.0001));
                float u = dot(pw, alongWall);

                half variation = _DetailAlbedoMapScale;
                half big = WetNoise(pw.xz * 0.09 + pw.y * 0.06) * 0.65 + WetNoise(float2(u, pw.y) * 0.33) * 0.35;
                surfaceData.albedo *= lerp(1.0, lerp(0.62, 1.15, big), variation);
                half warm = saturate(WetNoise(pw.xz * 0.045 + 17.3) * 1.6 - 0.5);
                surfaceData.albedo *= lerp(half3(1, 1, 1), half3(1.1, 1.0, 0.82), warm * variation);

                half cavity = 0;
            #if defined(_PARALLAXMAP)
                half h = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, input.uv).g;
                cavity = saturate((0.42 - h) * 3.0);
            #endif

                half weather = _ClearCoatMask;
                // (aged: a little greyer and darker all over, the brightest stone most)
                half grey = dot(surfaceData.albedo, half3(0.3, 0.59, 0.11));
                surfaceData.albedo = lerp(surfaceData.albedo, grey * half3(0.95, 0.94, 0.9), weather * 0.35) * (1.0 - weather * 0.12);
                half streak = saturate((WetNoise(float2(u * 1.3, pw.y * 0.035)) - 0.38) * 3.2) * wall;
                streak *= lerp(0.45, 1.0, WetNoise(float2(u * 7.0, pw.y * 0.22)));
                half dirt = saturate(streak * 0.85 + cavity * 0.6) * weather;
                surfaceData.albedo *= lerp(half3(1, 1, 1), half3(0.42, 0.44, 0.38), dirt);
                surfaceData.occlusion *= 1.0 - cavity * weather * 0.3;

                // (moss on tops: _ClearCoatSmoothness; in the joints: _SpecColor.a; how big its patches are: _DetailNormalMapScale)
                half mossAmount = _ClearCoatSmoothness, jointMoss = _SpecColor.a;
                float patch = 1.0 / max(0.2, _DetailNormalMapScale);
                half mossNoise = WetNoise(pw.xz * 0.3 * patch + pw.y * 0.19 * patch) * 0.6 + WetNoise(pw.xz * 1.6 * patch + pw.y * 1.2 * patch) * 0.4;
                half onTop = smoothstep(0.3, 0.75, upness) * saturate((mossNoise - (0.85 - mossAmount)) * 3.5) * step(0.001, mossAmount);
                half inJoints = cavity * saturate((mossNoise - (1.0 - jointMoss * 0.85)) * 4.0) * step(0.001, jointMoss);
                half moss = saturate(onTop + inJoints);
                half3 mossColour = _SpecColor.rgb * lerp(0.7, 1.2, WetNoise(pw.xz * 2.7 + pw.y));
                surfaceData.albedo = lerp(surfaceData.albedo, mossColour, moss);
                surfaceData.smoothness = lerp(surfaceData.smoothness, 0.04, moss);
            #endif

                // Wet: tops (roofs, walkways, sills) soak most, walls a little less; metal hardly darkens
                half up = saturate(inputData.normalWS.y);
                half wet = saturate(_Wetness) * lerp(0.75, 1.0, up);
                surfaceData.albedo = WetAlbedo(surfaceData.albedo, wet * 0.85 * (1.0 - surfaceData.metallic * 0.7));
                surfaceData.smoothness = lerp(surfaceData.smoothness, max(surfaceData.smoothness, lerp(0.6, 0.85, up)), wet);

                InitializeBakedGIData(input, inputData);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                // (a sharp glint of the sun or moon on wet surfaces, and the sky in them at low angles)
                color.rgb += WetShine(inputData.normalWS, inputData.viewDirectionWS, GetMainLight(inputData.shadowCoord), wet * 0.35, surfaceData.smoothness);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
                outColor = color;

            #ifdef _WRITE_RENDERING_LAYERS
                uint renderingLayers = GetMeshRenderingLayer();
                outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
            #endif
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
        UsePass "Universal Render Pipeline/Lit/META"
    }

    FallBack "Universal Render Pipeline/Lit"
}
