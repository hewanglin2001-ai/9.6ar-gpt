Shader "Hidden/Faceless/SurfaceGuard"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend Off
            ColorMask RGBA
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct VertexInput { float4 vertex : POSITION; };
            struct VertexOutput { float4 position : SV_POSITION; };

            VertexOutput vert(VertexInput inputVertex)
            {
                VertexOutput outputVertex;
                float2 clipPosition = inputVertex.vertex.xy * 2.0 - 1.0;
                // Direct rendering into a RenderTexture: align its sampled UVs
                // with the native camera UVs on Metal/D3D as well as OpenGL.
                #if UNITY_UV_STARTS_AT_TOP
                    clipPosition.y = -clipPosition.y;
                #endif
                outputVertex.position = float4(clipPosition, 0.0, 1.0);
                return outputVertex;
            }

            fixed4 frag(VertexOutput inputVertex) : SV_Target
            {
                return fixed4(1.0, 1.0, 1.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
