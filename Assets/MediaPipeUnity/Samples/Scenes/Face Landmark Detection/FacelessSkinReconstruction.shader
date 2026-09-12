Shader "Hidden/Faceless/SkinReconstruction"
{
    Properties { _MainTex ("Source", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "FacelessSkinCommon.cginc"
        sampler2D _MainTex, _KnownTex, _DonorTex, _HistoryTex;
        float4 _MainTex_TexelSize;
        float2 _Direction;
        float _TemporalWeight;

        float4 Gaussian(float2 uv)
        {
            // Five bilinear taps = nine contiguous Gaussian texel taps.
            // Never jump across a large radius in the original video.
            float2 d = _Direction * abs(_MainTex_TexelSize.xy);
            float4 c = tex2D(_MainTex, uv) * 0.2270270270;
            c += (tex2D(_MainTex, uv + d * 1.3846153846) +
                  tex2D(_MainTex, uv - d * 1.3846153846)) * 0.3162162162;
            c += (tex2D(_MainTex, uv + d * 3.2307692308) +
                  tex2D(_MainTex, uv - d * 3.2307692308)) * 0.0702702703;
            return c;
        }
        float4 fragDonors(v2f_img i) : SV_Target
        {
            int n = min((int)(i.uv.x * 6), 5);
            float2 c = _Donors[n].xy * _CameraSize.zw;
            float2 r = _Donors[n].z * _CameraSize.zw;
            float3 col = tex2D(_MainTex, c).rgb * 4.0;
            col += (tex2D(_MainTex,c+float2(r.x,0)).rgb + tex2D(_MainTex,c-float2(r.x,0)).rgb
                + tex2D(_MainTex,c+float2(0,r.y)).rgb + tex2D(_MainTex,c-float2(0,r.y)).rgb) * 2.0;
            col += tex2D(_MainTex,c+r).rgb + tex2D(_MainTex,c-r).rgb
                + tex2D(_MainTex,c+float2(r.x,-r.y)).rgb + tex2D(_MainTex,c+float2(-r.x,r.y)).rgb;
            return float4(col / 16.0, 1);
        }
        float4 fragSeeds(v2f_img i) : SV_Target
        {
            float2 p = AtlasToPixel(i.uv);
            float2 uv = p * _CameraSize.zw;
            if (any(uv < 0.0) || any(uv > 1.0)) return 0;
            float confidence = BoundaryGuard(p);
            [unroll] for (int n = 0; n < 7; n++)
            {
                // Feature pixels have ZERO source weight before any filtering.
                // Expanded source exclusion also removes eye-socket/lip shadows.
                float edge = max(_RegionAxes[n].z, 1.0);
                confidence *= smoothstep(0.0, edge * 0.45, RegionDistance(p, n));
            }
            float y = dot(p - _FrameOrigin.xy, normalize(_FrameV.xy));
            confidence *= 1.0 - smoothstep(_FrameV.z, _FrameV.z + _FrameOrigin.w * 0.025, y);
            float3 source = tex2D(_MainTex, uv).rgb;
            float3 cheek = 0;
            [unroll] for (int n = 0; n < 6; n++) cheek += tex2D(_DonorTex, float2((n+0.5)/6.0,0.5)).rgb / 6.0;
            // Luminance-relative rejection, no fixed skin-tone threshold.
            // Cheek color is a validity reference, never an opaque color overlay.
            float ratio = Luminance(source) / max(Luminance(cheek), 0.005);
            confidence *= smoothstep(0.24, 0.52, ratio) * (1.0 - smoothstep(2.5, 4.0, ratio));
            return float4(source * confidence, confidence);
        }
        float4 fragGaussian(v2f_img i) : SV_Target { return Gaussian(i.uv); }
        float4 fragNormalize(v2f_img i) : SV_Target
        {
            float4 moments = tex2D(_MainTex, i.uv);
            float3 fallback = 0;
            [unroll] for (int n = 0; n < 6; n++) fallback += tex2D(_DonorTex,float2((n+0.5)/6.0,0.5)).rgb / 6.0;
            return float4(moments.a > 0.00001 ? moments.rgb / moments.a : fallback, 1);
        }
        float4 fragPull(v2f_img i) : SV_Target
        {
            float4 known = tex2D(_KnownTex, i.uv);
            float3 smoothColor = tex2D(_MainTex, i.uv).rgb;
            return float4(lerp(smoothColor, known.rgb / max(known.a, 0.00001),
                smoothstep(0.0, 0.85, known.a)), 1);
        }
        float4 fragRelax(v2f_img i) : SV_Target
        {
            float4 known = tex2D(_KnownTex, i.uv);
            float3 smoothColor = Gaussian(i.uv).rgb;
            return float4(lerp(smoothColor, known.rgb / max(known.a, 0.00001),
                smoothstep(0.0, 0.85, known.a)), 1);
        }
        float4 fragTemporal(v2f_img i) : SV_Target
        {
            float3 current = tex2D(_MainTex, i.uv).rgb;
            float3 previous = tex2D(_HistoryTex, i.uv).rgb;
            float change = abs(Luminance(current) - Luminance(previous));
            // Face-local history, reset on loss/reacquisition. Exposure changes
            // accelerate convergence rather than leaving a gray trail.
            float t = lerp(_TemporalWeight, 1.0, smoothstep(0.035, 0.16, change));
            return float4(lerp(previous, current, t), 1);
        }
        ENDCG
        Pass // 0: compact cheek samples
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment fragDonors
            ENDCG
        }
        Pass // 1: premultiplied trusted skin + confidence, feature pixels excluded
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment fragSeeds
            ENDCG
        }
        Pass // 2: contiguous separable Gaussian for the confidence pyramid
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment fragGaussian
            ENDCG
        }
        Pass // 3: normalize coarsest reliable color
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment fragNormalize
            ENDCG
        }
        Pass // 4: pull reliable boundaries into finer scales
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment fragPull
            ENDCG
        }
        Pass // 5: relax interpolation while preserving reliable boundaries
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment fragRelax
            ENDCG
        }
        Pass // 6: stabilize only the reconstructed, face-local skin field
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment fragTemporal
            ENDCG
        }
    }
    Fallback Off
}
