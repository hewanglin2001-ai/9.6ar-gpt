Shader "Custom/FacelessBlur"
{
    Properties
    {
        _MainTex ("Camera Texture", 2D) = "white" {}

        _SkinColor ("Skin Color", Color) =
        (0.72, 0.55, 0.43, 1)

        _BlurRadius ("Blur Radius", Range(0.001, 0.20)) =
        0.10

        _Flatten ("Flatten", Range(0,1)) =
        0.90

        _BrightnessStrength ("Keep Lighting", Range(0,1)) =
        0.80
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+200"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float4 _MainTex_TexelSize;

            fixed4 _SkinColor;

            float _BlurRadius;
            float _Flatten;
            float _BrightnessStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex =
                    UnityObjectToClipPos(
                        v.vertex
                    );

                o.uv =
                    v.uv;

                o.color =
                    v.color;

                return o;
            }

            // =====================================================
            // 9 x 9 DENSE GAUSSIAN
            //
            // 和之前不同：
            // 采样点更密
            // 相邻采样距离更小
            // 避免眼睛嘴巴被复制成几层
            // =====================================================

            fixed3 DenseGaussian81(
                float2 uv,
                float radius
            )
            {
                float aspect =
                    _MainTex_TexelSize.x /
                    max(
                        _MainTex_TexelSize.y,
                        0.000001
                    );

                // 9个点覆盖整个 radius
                // 相邻距离明显比上一版小
                float2 stepUV =
                    float2(
                        radius *
                        aspect /
                        4.0,

                        radius /
                        4.0
                    );

                fixed3 sum =
                    fixed3(
                        0,
                        0,
                        0
                    );

                float total =
                    0.0;

                [unroll]
                for (
                    int y = -4;
                    y <= 4;
                    y++
                )
                {
                    [unroll]
                    for (
                        int x = -4;
                        x <= 4;
                        x++
                    )
                    {
                        float2 grid =
                            float2(
                                (float)x,
                                (float)y
                            );

                        // 距离平方
                        float d2 =
                            dot(
                                grid,
                                grid
                            );

                        // 连续 Gaussian 权重
                        // 中心强，外围逐渐变弱
                        float weight =
                            exp(
                                -d2 *
                                0.155
                            );

                        float2 offset =
                            float2(
                                stepUV.x * x,
                                stepUV.y * y
                            );

                        fixed3 c =
                            tex2D(
                                _MainTex,
                                saturate(
                                    uv +
                                    offset
                                )
                            ).rgb;

                        sum +=
                            c *
                            weight;

                        total +=
                            weight;
                    }
                }

                return
                    sum /
                    max(
                        total,
                        0.0001
                    );
            }

            // =====================================================
            // ROTATED SMEAR
            //
            // 使用不同角度、不同距离的采样
            // 不沿规则横竖线复制五官
            // =====================================================

            fixed3 RotatedSmear(
                float2 uv,
                float radius
            )
            {
                float aspect =
                    _MainTex_TexelSize.x /
                    max(
                        _MainTex_TexelSize.y,
                        0.000001
                    );

                fixed3 total =
                    tex2D(
                        _MainTex,
                        uv
                    ).rgb * 0.12;

                float weightTotal =
                    0.12;

                // ---------------------------------------------
                // 第一圈
                // 很靠近中心
                // ---------------------------------------------

                const float r1 =
                    0.18;

                const float w1 =
                    0.055;

                float2 a1 =
                    float2(
                        0.9239,
                        0.3827
                    );

                float2 a2 =
                    float2(
                        0.3827,
                        0.9239
                    );

                float2 a3 =
                    float2(
                        -0.3827,
                        0.9239
                    );

                float2 a4 =
                    float2(
                        -0.9239,
                        0.3827
                    );

                float2 a5 =
                    -a1;

                float2 a6 =
                    -a2;

                float2 a7 =
                    -a3;

                float2 a8 =
                    -a4;

                float2 scale =
                    float2(
                        radius *
                        aspect,

                        radius
                    );

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a1 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a2 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a3 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a4 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a5 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a6 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a7 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a8 *
                            scale *
                            r1
                        )
                    ).rgb * w1;

                weightTotal +=
                    8.0 *
                    w1;

                // ---------------------------------------------
                // 第二圈
                // 角度错开
                // ---------------------------------------------

                const float r2 =
                    0.36;

                const float w2 =
                    0.036;

                float2 b1 =
                    float2(
                        1.0,
                        0.0
                    );

                float2 b2 =
                    float2(
                        0.7071,
                        0.7071
                    );

                float2 b3 =
                    float2(
                        0.0,
                        1.0
                    );

                float2 b4 =
                    float2(
                        -0.7071,
                        0.7071
                    );

                float2 b5 =
                    -b1;

                float2 b6 =
                    -b2;

                float2 b7 =
                    -b3;

                float2 b8 =
                    -b4;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b1 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b2 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b3 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b4 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b5 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b6 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b7 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            b8 *
                            scale *
                            r2
                        )
                    ).rgb * w2;

                weightTotal +=
                    8.0 *
                    w2;

                // ---------------------------------------------
                // 第三圈
                //
                // 只占很小权重
                // 用于继续融掉嘴、鼻孔、眼睛
                // ---------------------------------------------

                const float r3 =
                    0.58;

                const float w3 =
                    0.018;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a1 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a2 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a3 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a4 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a5 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a6 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a7 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                total +=
                    tex2D(
                        _MainTex,
                        saturate(
                            uv +
                            a8 *
                            scale *
                            r3
                        )
                    ).rgb * w3;

                weightTotal +=
                    8.0 *
                    w3;

                return
                    total /
                    weightTotal;
            }

            // =====================================================
            // FRAGMENT
            // =====================================================

            fixed4 frag(
                v2f i
            ) : SV_Target
            {
                // =================================================
                // 主要平滑
                //
                // 强度比上一版提高
                // 但采样更密
                // =================================================

                fixed3 denseBlur =
                    DenseGaussian81(
                        i.uv,

                        _BlurRadius *
                        1.30
                    );

                // =================================================
                // 涂抹
                //
                // 强化融化感觉
                // =================================================

                fixed3 smear =
                    RotatedSmear(
                        i.uv,

                        _BlurRadius *
                        1.72
                    );

                // =================================================
                // 以密集Gaussian作为主体
                // =================================================

                fixed3 blurredFace =
                    denseBlur *
                    0.72
                    +
                    smear *
                    0.28;

                // =================================================
                // 再做一次低频融合
                //
                // 进一步压掉眼睛/嘴/鼻孔的局部对比
                // =================================================

                fixed3 lowFrequency =
                    DenseGaussian81(
                        i.uv,

                        _BlurRadius *
                        1.62
                    );

                blurredFace =
                    lerp(
                        blurredFace,
                        lowFrequency,
                        0.34
                    );

                // =================================================
                // 大尺度亮度
                // =================================================

                float faceLum =
                    dot(
                        lowFrequency,

                        fixed3(
                            0.299,
                            0.587,
                            0.114
                        )
                    );

                float skinLum =
                    max(
                        dot(
                            _SkinColor.rgb,

                            fixed3(
                                0.299,
                                0.587,
                                0.114
                            )
                        ),

                        0.05
                    );

                float lighting =
                    faceLum /
                    skinLum;

                lighting =
                    clamp(
                        lighting,
                        0.68,
                        1.34
                    );

                // =================================================
                // 实时肤色
                //
                // 只轻度参与
                // 避免再次成为纯色面具
                // =================================================

                fixed3 sampledSkin =
                    _SkinColor.rgb *
                    lighting;

                float skinMix =
                    lerp(
                        0.08,
                        0.22,
                        _Flatten
                    );

                fixed3 result =
                    lerp(
                        blurredFace,
                        sampledSkin,
                        skinMix
                    );

                // =================================================
                // 进一步消除局部纹理
                //
                // 这一步产生更明显的“涂抹”
                // =================================================

                float smoothing =
                    lerp(
                        0.15,
                        0.32,
                        _Flatten
                    );

                result =
                    lerp(
                        result,
                        lowFrequency,
                        smoothing
                    );

                // =================================================
                // 保留大尺度光影
                // =================================================

                float resultLum =
                    max(
                        dot(
                            result,

                            fixed3(
                                0.299,
                                0.587,
                                0.114
                            )
                        ),

                        0.01
                    );

                float lumCorrection =
                    faceLum /
                    resultLum;

                lumCorrection =
                    clamp(
                        lumCorrection,
                        0.84,
                        1.18
                    );

                lumCorrection =
                    lerp(
                        1.0,
                        lumCorrection,
                        _BrightnessStrength
                    );

                result *=
                    lumCorrection;

                result =
                    saturate(
                        result
                    );

                // =================================================
                // 使用原来的脸部羽化边缘
                // =================================================

                return
                    fixed4(
                        result,
                        i.color.a
                    );
            }

            ENDCG
        }
    }
}