Shader "Faceless/SkinComposite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Camera", 2D) = "white" {}
        _SkinTex ("Reconstructed skin", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil operation", Float) = 0
        _StencilWriteMask ("Stencil write mask", Float) = 255
        _StencilReadMask ("Stencil read mask", Float) = 255
        _ColorMask ("Color mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "FacelessSkinCommon.cginc"
            sampler2D _MainTex, _SkinTex;
            float4 _Color, _ClipRect, _FaceBounds;
            float _Amount, _Volume, _Grain, _ShowMask;
            float _Stages[7];
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 local:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 original = tex2D(_MainTex, i.uv);
                float2 p = i.uv * _CameraSize.xy;
                float2 atlas = PixelToAtlas(p);
                float amount = 0;
                float4 outputColor = original;
                if (_Amount > 0 && all(atlas >= 0) && all(atlas <= 1))
                {
                    float uncovered = 1.0;
                    [unroll] for (int n=0;n<7;n++) uncovered *= 1.0-RegionAlpha(p,n)*_Stages[n];
                    amount = (1.0-uncovered) * BoundaryGuard(p) * _Amount;
                    if (amount > 0.00001)
                    {
                        float3 skin = tex2D(_SkinTex,atlas).rgb;
                        float2 q = (atlas-0.5)*2.0;
                        // A very gentle broad highlight; no nose/eye structure.
                        skin *= 1.0 + _Volume * exp(-3.0*dot(q,q));
                        float grain = frac(sin(dot(floor(atlas*768.0),float2(12.9898,78.233)))*43758.5453)-0.5;
                        skin += grain * (_Grain / 255.0);
                        outputColor.rgb = lerp(original.rgb, saturate(skin), amount);
                    }
                    if (_ShowMask > 0.5)
                    {
                        outputColor.rgb = lerp(outputColor.rgb,float3(0.1,0.8,0.65),amount*0.45);
                        [unroll] for (int n=0;n<7;n++)
                        {
                            float d=RegionDistance(p,n);
                            float regionOutline=1.0-smoothstep(0.4,1.6,min(abs(d),abs(d-_RegionAxes[n].z)));
                            float dotCenter=1.0-smoothstep(1.2,2.5,length(p-_Regions[n].xy));
                            outputColor.rgb=lerp(outputColor.rgb,float3(1,0.8,0.15),max(regionOutline,dotCenter));
                        }
                        float bound=1.0-smoothstep(0.4,1.6,abs(BoundaryDistance(p)));
                        outputColor.rgb=lerp(outputColor.rgb,float3(0.1,0.65,1),bound);
                        float2 box=abs(p-(_FaceBounds.xy+_FaceBounds.zw)*0.5)-(_FaceBounds.zw-_FaceBounds.xy)*0.5;
                        float boxDistance=length(max(box,0.0))+min(max(box.x,box.y),0.0);
                        float boxLine=1.0-smoothstep(0.4,1.6,abs(boxDistance));
                        outputColor.rgb=lerp(outputColor.rgb,float3(0.2,0.5,1),boxLine);
                    }
                }
                outputColor *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                outputColor.a *= UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(outputColor.a-0.001);
                #endif
                return outputColor;
            }
            ENDCG
        }
    }
    Fallback Off
}
