using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MidFaceEraseMask : MonoBehaviour
{
    // ============================================================
    // AUTO REFERENCES
    // 自动寻找，不需要手动拖
    // ============================================================

    [Header("Auto References")]
    public Transform pointListAnnotation;
    public RawImage screenImage;

    // ============================================================
    // LOCAL SMEAR MASK
    // 局部涂抹设置
    // ============================================================

    [Header("Local Smear Mask")]

    // 左右眼区域
    public float eyeWidthScale = 1.70f;
    public float eyeHeightScale = 2.10f;

    // 鼻子
    public float noseWidthScale = 1.55f;
    public float noseHeightScale = 1.18f;

    // 嘴
    public float mouthWidthScale = 1.32f;
    public float mouthHeightScale = 3.20f;

    // 中央鼻梁 / T区
    public float bridgeWidthRelative = 0.24f;
    public float bridgeHeightRelative = 0.34f;

    // 鼻子到嘴的区域
    public float midLowerWidthRelative = 0.29f;
    public float midLowerHeightRelative = 0.25f;

    // 下巴中央
    public float chinWidthRelative = 0.29f;
    public float chinHeightRelative = 0.15f;

    // 每个局部区域外围的柔软羽化
    public float feather = 0.26f;

    // 椭圆精细程度
    public int ellipseSegments = 40;

    public float zOffset = -10f;

    // ============================================================
    // TRACKING
    // ============================================================

    [Header("Tracking")]

    public float smoothSpeed = 28f;

    // ============================================================
    // FACELESS EFFECT
    //
    // 这里故意比之前整脸版本的 Blur Radius 小
    // 因为现在是在真正的五官局部处理
    // 可以减少之前出现的重复眼睛 / 马赛克
    // ============================================================

    [Header("Faceless Effect")]

    [Range(0.02f, 0.15f)]
    public float blurRelativeToFace = 0.070f;

    [Range(0f, 1f)]
    public float flatten = 0.92f;

    [Range(0f, 1f)]
    public float keepLighting = 0.88f;

    // ============================================================
    // SKIN COLOR
    // ============================================================

    [Header("Skin Color Sampling")]

    public bool enableSkinSampling = true;

    public float sampleInterval = 0.10f;

    public int sampleRadius = 4;

    public float skinColorSmooth = 0.22f;

    // ============================================================
    // MEDIAPIPE VISUALS
    // ============================================================

    [Header("MediaPipe Visuals")]

    public bool hideLandmarkVisuals = true;

    public float hideVisualInterval = 0.25f;

    // ============================================================
    // DEBUG
    // ============================================================

    [Header("Debug")]

    public Color sampledSkinColor =
        new Color(
            0.72f,
            0.55f,
            0.43f,
            1f
        );

    // ============================================================
    // INTERNAL
    // ============================================================

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;

    private Vector3[] smoothedVertices;

    private float nextSampleTime = 0f;
    private float nextHideTime = 0f;

    // ============================================================
    // SKIN SAMPLE POINTS
    //
    // 全部从真正脸颊取样，
    // 不从眼睛、嘴、头发区域取颜色。
    // ============================================================

    private readonly int[] skinSampleIndices =
    {
        50,
        101,
        205,

        280,
        330,
        425
    };

    // ============================================================
    // AWAKE
    // ============================================================

    void Awake()
    {
        mesh =
            new Mesh();

        mesh.name =
            "Landmark Local Smear Mask";

        MeshFilter filter =
            GetComponent<MeshFilter>();

        filter.mesh =
            mesh;

        meshRenderer =
            GetComponent<MeshRenderer>();

        Shader shader =
            Shader.Find(
                "Custom/FacelessBlur"
            );

        if (shader == null)
        {
            Debug.LogError(
                "MidFaceEraseMask: 找不到 Custom/FacelessBlur Shader"
            );

            return;
        }

        runtimeMaterial =
            new Material(shader);

        meshRenderer.material =
            runtimeMaterial;
    }

    // ============================================================
    // UPDATE
    // ============================================================

    void Update()
    {
        AutoFindReferences();

        if (
            pointListAnnotation == null ||
            screenImage == null
        )
            return;

        if (
            pointListAnnotation.childCount < 468
        )
            return;

        AttachToLandmarks();

        BuildLocalSmearMask();

        UpdateShader();

        if (
            hideLandmarkVisuals &&
            Time.time >= nextHideTime
        )
        {
            HideMediaPipeVisuals();

            nextHideTime =
                Time.time +
                hideVisualInterval;
        }

        if (
            enableSkinSampling &&
            Time.time >= nextSampleTime
        )
        {
            SampleSkinColor();

            nextSampleTime =
                Time.time +
                sampleInterval;
        }
    }

    // ============================================================
    // AUTO FIND REFERENCES
    // ============================================================

    void AutoFindReferences()
    {
        // --------------------------------------------------------
        // 找 FaceLandmarkList Annotation
        // 下面真正的 Point List Annotation
        // --------------------------------------------------------

        if (pointListAnnotation == null)
        {
            GameObject[] objects =
                FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (GameObject obj in objects)
            {
                if (
                    obj.name !=
                    "Point List Annotation"
                )
                    continue;

                Transform t =
                    obj.transform;

                if (t.parent == null)
                    continue;

                if (
                    t.parent.name !=
                    "FaceLandmarkList Annotation"
                )
                    continue;

                if (
                    t.childCount < 468
                )
                    continue;

                pointListAnnotation =
                    t;

                Debug.Log(
                    "MidFaceEraseMask: Found Face Point List Annotation."
                );

                break;
            }
        }

        // --------------------------------------------------------
        // 找摄像头画面 RawImage
        // --------------------------------------------------------

        if (screenImage == null)
        {
            RawImage[] images =
                FindObjectsByType<RawImage>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (RawImage img in images)
            {
                Transform current =
                    img.transform;

                while (current != null)
                {
                    if (
                        current.name.Contains(
                            "Annotatable Screen"
                        )
                    )
                    {
                        screenImage =
                            img;

                        Debug.Log(
                            "MidFaceEraseMask: Found Annotatable Screen."
                        );

                        return;
                    }

                    current =
                        current.parent;
                }
            }
        }
    }

    // ============================================================
    // ATTACH
    // ============================================================

    void AttachToLandmarks()
    {
        if (
            transform.parent ==
            pointListAnnotation
        )
            return;

        transform.SetParent(
            pointListAnnotation,
            false
        );

        transform.localPosition =
            Vector3.zero;

        transform.localRotation =
            Quaternion.identity;

        transform.localScale =
            Vector3.one;
    }

    // ============================================================
    // LANDMARK
    // ============================================================

    Vector3 Landmark(int index)
    {
        Vector3 p =
            pointListAnnotation
            .GetChild(index)
            .localPosition;

        p.z =
            zOffset;

        return p;
    }

    // ============================================================
    // AVERAGE LANDMARK
    // ============================================================

    Vector3 Average(params int[] indices)
    {
        Vector3 result =
            Vector3.zero;

        foreach (
            int index
            in indices
        )
        {
            result +=
                Landmark(index);
        }

        result /=
            indices.Length;

        result.z =
            zOffset;

        return result;
    }

    // ============================================================
    // DISTANCE
    // ============================================================

    float Distance2D(
        Vector3 a,
        Vector3 b
    )
    {
        return
            Vector2.Distance(
                new Vector2(
                    a.x,
                    a.y
                ),
                new Vector2(
                    b.x,
                    b.y
                )
            );
    }

    // ============================================================
    // BUILD LOCAL MASK
    // ============================================================

    void BuildLocalSmearMask()
    {
        // --------------------------------------------------------
        // 整张脸尺寸只用来决定相对大小，
        // 不再作为遮罩轮廓。
        // --------------------------------------------------------

        Vector3 leftFace =
            Landmark(234);

        Vector3 rightFace =
            Landmark(454);

        Vector3 top =
            Landmark(10);

        Vector3 chin =
            Landmark(152);

        float faceWidth =
            Distance2D(
                leftFace,
                rightFace
            );

        float faceHeight =
            Distance2D(
                top,
                chin
            );

        if (
            faceWidth < 0.001f ||
            faceHeight < 0.001f
        )
            return;

        // ========================================================
        // 脸部坐标轴
        //
        // 这样歪头之后局部遮罩也会一起旋转。
        // ========================================================

        Vector2 horizontal =
            new Vector2(
                rightFace.x - leftFace.x,
                rightFace.y - leftFace.y
            ).normalized;

        Vector2 vertical =
            new Vector2(
                chin.x - top.x,
                chin.y - top.y
            ).normalized;

        // ========================================================
        // 所有区域
        // ========================================================

        List<Region> regions =
            new List<Region>();

        // ========================================================
        // 1. 左眼 + 左眉
        // ========================================================

        Vector3 leftEyeCenter =
            Average(
                33,
                133,
                159,
                145
            );

        Vector3 leftBrowCenter =
            Average(
                70,
                63,
                105,
                66
            );

        Vector3 leftEyeBrowCenter =
            Vector3.Lerp(
                leftEyeCenter,
                leftBrowCenter,
                0.38f
            );

        float leftEyeWidth =
            Distance2D(
                Landmark(33),
                Landmark(133)
            )
            *
            eyeWidthScale;

        float leftEyeHeight =
            Mathf.Max(
                Distance2D(
                    Landmark(159),
                    Landmark(145)
                )
                *
                eyeHeightScale,

                faceHeight *
                0.075f
            );

        regions.Add(
            new Region(
                leftEyeBrowCenter,
                leftEyeWidth * 0.5f,
                leftEyeHeight * 0.5f
            )
        );

        // ========================================================
        // 2. 右眼 + 右眉
        // ========================================================

        Vector3 rightEyeCenter =
            Average(
                362,
                263,
                386,
                374
            );

        Vector3 rightBrowCenter =
            Average(
                300,
                293,
                334,
                296
            );

        Vector3 rightEyeBrowCenter =
            Vector3.Lerp(
                rightEyeCenter,
                rightBrowCenter,
                0.38f
            );

        float rightEyeWidth =
            Distance2D(
                Landmark(362),
                Landmark(263)
            )
            *
            eyeWidthScale;

        float rightEyeHeight =
            Mathf.Max(
                Distance2D(
                    Landmark(386),
                    Landmark(374)
                )
                *
                eyeHeightScale,

                faceHeight *
                0.075f
            );

        regions.Add(
            new Region(
                rightEyeBrowCenter,
                rightEyeWidth * 0.5f,
                rightEyeHeight * 0.5f
            )
        );

        // ========================================================
        // 3. 鼻梁中央
        //
        // 直接由两眼中间到鼻子决定，
        // 不使用脸边缘。
        // ========================================================

        Vector3 bridgeTop =
            Average(
                168,
                6
            );

        Vector3 bridgeBottom =
            Average(
                1,
                4
            );

        Vector3 bridgeCenter =
            Vector3.Lerp(
                bridgeTop,
                bridgeBottom,
                0.52f
            );

        regions.Add(
            new Region(
                bridgeCenter,

                faceWidth *
                bridgeWidthRelative *
                0.5f,

                faceHeight *
                bridgeHeightRelative *
                0.5f
            )
        );

        // ========================================================
        // 4. 鼻子
        // ========================================================

        Vector3 noseCenter =
            Average(
                1,
                2,
                4,
                5
            );

        float noseWidth =
            Distance2D(
                Landmark(98),
                Landmark(327)
            )
            *
            noseWidthScale;

        float noseHeight =
            Distance2D(
                Landmark(168),
                Landmark(2)
            )
            *
            noseHeightScale;

        regions.Add(
            new Region(
                noseCenter,
                noseWidth * 0.5f,
                noseHeight * 0.5f
            )
        );

        // ========================================================
        // 5. 鼻子 → 嘴巴 / 法令纹中央区域
        //
        // 这是之前容易露出法令纹的位置。
        // ========================================================

        Vector3 upperLip =
            Average(
                0,
                13
            );

        Vector3 midLowerCenter =
            Vector3.Lerp(
                Landmark(2),
                upperLip,
                0.58f
            );

        regions.Add(
            new Region(
                midLowerCenter,

                faceWidth *
                midLowerWidthRelative *
                0.5f,

                faceHeight *
                midLowerHeightRelative *
                0.5f
            )
        );

        // ========================================================
        // 6. 嘴巴
        // ========================================================

        Vector3 mouthCenter =
            Average(
                61,
                291,
                13,
                14
            );

        float mouthWidth =
            Distance2D(
                Landmark(61),
                Landmark(291)
            )
            *
            mouthWidthScale;

        float mouthHeight =
            Mathf.Max(
                Distance2D(
                    Landmark(13),
                    Landmark(14)
                )
                *
                mouthHeightScale,

                faceHeight *
                0.070f
            );

        regions.Add(
            new Region(
                mouthCenter,
                mouthWidth * 0.5f,
                mouthHeight * 0.5f
            )
        );

        // ========================================================
        // 7. 下巴中央
        //
        // 只处理中央。
        // 下颚角不会碰。
        // ========================================================

        Vector3 chinCenter =
            Vector3.Lerp(
                Average(
                    17,
                    18
                ),
                Landmark(152),
                0.58f
            );

        regions.Add(
            new Region(
                chinCenter,

                faceWidth *
                chinWidthRelative *
                0.5f,

                faceHeight *
                chinHeightRelative *
                0.5f
            )
        );

        // ========================================================
        // 最终生成一个 Mesh
        //
        // 它实际上由多个互不相连的柔软椭圆组成。
        // ========================================================

        BuildMesh(
            regions,
            horizontal,
            vertical
        );
    }

    // ============================================================
    // REGION
    // ============================================================

    private struct Region
    {
        public Vector3 center;
        public float halfWidth;
        public float halfHeight;

        public Region(
            Vector3 center,
            float halfWidth,
            float halfHeight
        )
        {
            this.center =
                center;

            this.halfWidth =
                halfWidth;

            this.halfHeight =
                halfHeight;
        }
    }

    // ============================================================
    // BUILD MESH
    // ============================================================

    void BuildMesh(
        List<Region> regions,
        Vector2 horizontal,
        Vector2 vertical
    )
    {
        ellipseSegments =
            Mathf.Clamp(
                ellipseSegments,
                20,
                64
            );

        int verticesPerRegion =
            1 +
            ellipseSegments +
            ellipseSegments;

        int totalVertexCount =
            regions.Count *
            verticesPerRegion;

        List<Vector3> targetVertices =
            new List<Vector3>(
                totalVertexCount
            );

        List<Color> colors =
            new List<Color>(
                totalVertexCount
            );

        List<Vector2> uvs =
            new List<Vector2>(
                totalVertexCount
            );

        List<int> triangles =
            new List<int>();

        // --------------------------------------------------------
        // 一个一个生成局部椭圆
        // --------------------------------------------------------

        foreach (
            Region region
            in regions
        )
        {
            int baseIndex =
                targetVertices.Count;

            // ====================================================
            // CENTER
            // ====================================================

            Vector3 center =
                region.center;

            center.z =
                zOffset;

            targetVertices.Add(
                center
            );

            colors.Add(
                Color.white
            );

            uvs.Add(
                Vector2.zero
            );

            // ====================================================
            // INNER RING
            // 100% 特效
            // ====================================================

            for (
                int i = 0;
                i < ellipseSegments;
                i++
            )
            {
                float angle =
                    (
                        i /
                        (float)ellipseSegments
                    )
                    *
                    Mathf.PI *
                    2f;

                float cos =
                    Mathf.Cos(angle);

                float sin =
                    Mathf.Sin(angle);

                Vector3 p =
                    center;

                p.x +=
                    horizontal.x *
                    cos *
                    region.halfWidth;

                p.y +=
                    horizontal.y *
                    cos *
                    region.halfWidth;

                p.x +=
                    vertical.x *
                    sin *
                    region.halfHeight;

                p.y +=
                    vertical.y *
                    sin *
                    region.halfHeight;

                p.z =
                    zOffset;

                targetVertices.Add(
                    p
                );

                colors.Add(
                    new Color(
                        1f,
                        1f,
                        1f,
                        1f
                    )
                );

                uvs.Add(
                    Vector2.zero
                );
            }

            // ====================================================
            // OUTER FEATHER RING
            // ====================================================

            for (
                int i = 0;
                i < ellipseSegments;
                i++
            )
            {
                float angle =
                    (
                        i /
                        (float)ellipseSegments
                    )
                    *
                    Mathf.PI *
                    2f;

                float cos =
                    Mathf.Cos(angle);

                float sin =
                    Mathf.Sin(angle);

                float outerWidth =
                    region.halfWidth *
                    (
                        1f +
                        feather
                    );

                float outerHeight =
                    region.halfHeight *
                    (
                        1f +
                        feather
                    );

                Vector3 p =
                    center;

                p.x +=
                    horizontal.x *
                    cos *
                    outerWidth;

                p.y +=
                    horizontal.y *
                    cos *
                    outerWidth;

                p.x +=
                    vertical.x *
                    sin *
                    outerHeight;

                p.y +=
                    vertical.y *
                    sin *
                    outerHeight;

                p.z =
                    zOffset;

                targetVertices.Add(
                    p
                );

                colors.Add(
                    new Color(
                        1f,
                        1f,
                        1f,
                        0f
                    )
                );

                uvs.Add(
                    Vector2.zero
                );
            }

            // ====================================================
            // CENTER -> INNER
            // ====================================================

            for (
                int i = 0;
                i < ellipseSegments;
                i++
            )
            {
                int next =
                    (
                        i + 1
                    )
                    %
                    ellipseSegments;

                triangles.Add(
                    baseIndex
                );

                triangles.Add(
                    baseIndex +
                    1 +
                    i
                );

                triangles.Add(
                    baseIndex +
                    1 +
                    next
                );
            }

            // ====================================================
            // INNER -> OUTER FEATHER
            // ====================================================

            int innerStart =
                baseIndex + 1;

            int outerStart =
                innerStart +
                ellipseSegments;

            for (
                int i = 0;
                i < ellipseSegments;
                i++
            )
            {
                int next =
                    (
                        i + 1
                    )
                    %
                    ellipseSegments;

                int innerA =
                    innerStart + i;

                int innerB =
                    innerStart + next;

                int outerA =
                    outerStart + i;

                int outerB =
                    outerStart + next;

                triangles.Add(
                    innerA
                );

                triangles.Add(
                    outerA
                );

                triangles.Add(
                    outerB
                );

                triangles.Add(
                    innerA
                );

                triangles.Add(
                    outerB
                );

                triangles.Add(
                    innerB
                );
            }
        }

        // ========================================================
        // SMOOTH
        // ========================================================

        Vector3[] target =
            targetVertices.ToArray();

        if (
            smoothedVertices == null ||
            smoothedVertices.Length !=
            target.Length
        )
        {
            smoothedVertices =
                new Vector3[
                    target.Length
                ];

            for (
                int i = 0;
                i < target.Length;
                i++
            )
            {
                smoothedVertices[i] =
                    target[i];
            }
        }
        else
        {
            float t =
                1f -
                Mathf.Exp(
                    -smoothSpeed *
                    Time.deltaTime
                );

            for (
                int i = 0;
                i < target.Length;
                i++
            )
            {
                smoothedVertices[i] =
                    Vector3.Lerp(
                        smoothedVertices[i],
                        target[i],
                        t
                    );
            }
        }

        // ========================================================
        // UV
        // ========================================================

        Vector2[] uvArray =
            new Vector2[
                smoothedVertices.Length
            ];

        for (
            int i = 0;
            i < smoothedVertices.Length;
            i++
        )
        {
            uvArray[i] =
                LocalToCameraUV(
                    smoothedVertices[i]
                );
        }

        // ========================================================
        // APPLY
        // ========================================================

        mesh.Clear();

        mesh.vertices =
            smoothedVertices;

        mesh.colors =
            colors.ToArray();

        mesh.uv =
            uvArray;

        mesh.triangles =
            triangles.ToArray();

        mesh.RecalculateBounds();
    }

    // ============================================================
    // UPDATE SHADER
    // ============================================================

    void UpdateShader()
    {
        if (
            runtimeMaterial == null ||
            screenImage == null ||
            screenImage.texture == null
        )
            return;

        runtimeMaterial.SetTexture(
            "_MainTex",
            screenImage.texture
        );

        runtimeMaterial.SetColor(
            "_SkinColor",
            sampledSkinColor
        );

        runtimeMaterial.SetFloat(
            "_Flatten",
            flatten
        );

        runtimeMaterial.SetFloat(
            "_BrightnessStrength",
            keepLighting
        );

        Vector2 topUV =
            LocalToCameraUV(
                Landmark(10)
            );

        Vector2 chinUV =
            LocalToCameraUV(
                Landmark(152)
            );

        float faceHeightUV =
            Mathf.Abs(
                topUV.y -
                chinUV.y
            );

        float radius =
            faceHeightUV *
            blurRelativeToFace;

        runtimeMaterial.SetFloat(
            "_BlurRadius",
            radius
        );
    }

    // ============================================================
    // SKIN COLOR SAMPLING
    // ============================================================

    void SampleSkinColor()
    {
        if (
            screenImage == null ||
            screenImage.texture == null
        )
            return;

        Color total =
            Color.black;

        int validCount =
            0;

        foreach (
            int index
            in skinSampleIndices
        )
        {
            Vector2 uv =
                LocalToCameraUV(
                    Landmark(index)
                );

            Color color;

            if (
                !TrySampleTexture(
                    screenImage.texture,
                    uv,
                    out color
                )
            )
                continue;

            float brightness =
                (
                    color.r +
                    color.g +
                    color.b
                )
                /
                3f;

            if (
                brightness < 0.12f ||
                brightness > 0.95f
            )
                continue;

            Color.RGBToHSV(
                color,
                out float h,
                out float s,
                out float v
            );

            if (
                s > 0.78f
            )
                continue;

            total +=
                color;

            validCount++;
        }

        if (
            validCount == 0
        )
            return;

        Color target =
            total /
            validCount;

        target.a =
            1f;

        sampledSkinColor =
            Color.Lerp(
                sampledSkinColor,
                target,
                skinColorSmooth
            );
    }

    // ============================================================
    // READ TEXTURE
    // ============================================================

    bool TrySampleTexture(
        Texture texture,
        Vector2 uv,
        out Color result
    )
    {
        result =
            sampledSkinColor;

        uv.x =
            Mathf.Clamp01(
                uv.x
            );

        uv.y =
            Mathf.Clamp01(
                uv.y
            );

        // --------------------------------------------------------
        // WebCamTexture
        // --------------------------------------------------------

        if (
            texture is
            WebCamTexture webcam
        )
        {
            if (
                !webcam.isPlaying ||
                webcam.width < 32 ||
                webcam.height < 32
            )
                return false;

            int cx =
                Mathf.RoundToInt(
                    uv.x *
                    (
                        webcam.width - 1
                    )
                );

            int cy =
                Mathf.RoundToInt(
                    uv.y *
                    (
                        webcam.height - 1
                    )
                );

            Color total =
                Color.black;

            int count =
                0;

            for (
                int y = -sampleRadius;
                y <= sampleRadius;
                y++
            )
            {
                for (
                    int x = -sampleRadius;
                    x <= sampleRadius;
                    x++
                )
                {
                    int px =
                        Mathf.Clamp(
                            cx + x,
                            0,
                            webcam.width - 1
                        );

                    int py =
                        Mathf.Clamp(
                            cy + y,
                            0,
                            webcam.height - 1
                        );

                    total +=
                        webcam.GetPixel(
                            px,
                            py
                        );

                    count++;
                }
            }

            if (
                count == 0
            )
                return false;

            result =
                total /
                count;

            return true;
        }

        // --------------------------------------------------------
        // Texture2D
        // --------------------------------------------------------

        if (
            texture is
            Texture2D tex &&
            tex.isReadable
        )
        {
            int cx =
                Mathf.RoundToInt(
                    uv.x *
                    (
                        tex.width - 1
                    )
                );

            int cy =
                Mathf.RoundToInt(
                    uv.y *
                    (
                        tex.height - 1
                    )
                );

            Color total =
                Color.black;

            int count =
                0;

            for (
                int y = -sampleRadius;
                y <= sampleRadius;
                y++
            )
            {
                for (
                    int x = -sampleRadius;
                    x <= sampleRadius;
                    x++
                )
                {
                    int px =
                        Mathf.Clamp(
                            cx + x,
                            0,
                            tex.width - 1
                        );

                    int py =
                        Mathf.Clamp(
                            cy + y,
                            0,
                            tex.height - 1
                        );

                    total +=
                        tex.GetPixel(
                            px,
                            py
                        );

                    count++;
                }
            }

            if (
                count == 0
            )
                return false;

            result =
                total /
                count;

            return true;
        }

        return false;
    }

    // ============================================================
    // LOCAL -> CAMERA UV
    // ============================================================

    Vector2 LocalToCameraUV(
        Vector3 localPosition
    )
    {
        Vector3 world =
            transform.TransformPoint(
                localPosition
            );

        Canvas canvas =
            screenImage.canvas;

        Camera uiCamera =
            null;

        if (
            canvas != null &&
            canvas.renderMode !=
            RenderMode.ScreenSpaceOverlay
        )
        {
            uiCamera =
                canvas.worldCamera;
        }

        Vector2 screenPoint =
            RectTransformUtility
            .WorldToScreenPoint(
                uiCamera,
                world
            );

        Vector2 localPoint;

        bool ok =
            RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                screenImage.rectTransform,
                screenPoint,
                uiCamera,
                out localPoint
            );

        if (!ok)
            return Vector2.zero;

        Rect rect =
            screenImage.rectTransform.rect;

        float u =
            Mathf.InverseLerp(
                rect.xMin,
                rect.xMax,
                localPoint.x
            );

        float v =
            Mathf.InverseLerp(
                rect.yMin,
                rect.yMax,
                localPoint.y
            );

        Rect uvRect =
            screenImage.uvRect;

        u =
            uvRect.x +
            u *
            uvRect.width;

        v =
            uvRect.y +
            v *
            uvRect.height;

        return
            new Vector2(
                u,
                v
            );
    }

    // ============================================================
    // HIDE MEDIAPIPE LANDMARK VISUALS
    // ============================================================

    void HideMediaPipeVisuals()
    {
        if (
            pointListAnnotation == null
        )
            return;

        Transform root =
            pointListAnnotation.parent;

        if (root == null)
            return;

        if (
            root.parent != null &&
            root.parent.name.Contains(
                "FaceLandmarkListWithIris"
            )
        )
        {
            root =
                root.parent;
        }

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (
            Renderer renderer
            in renderers
        )
        {
            if (
                renderer == null
            )
                continue;

            if (
                renderer.gameObject ==
                gameObject
            )
                continue;

            if (
                renderer.GetComponent<MidFaceEraseMask>()
                != null
            )
                continue;

            string path =
                GetHierarchyPath(
                    renderer.transform
                );

            bool isLandmark =
                path.Contains(
                    "Point Annotation"
                )
                ||
                path.Contains(
                    "Point List Annotation"
                )
                ||
                path.Contains(
                    "Connection"
                )
                ||
                path.Contains(
                    "IrisLandmark"
                )
                ||
                path.Contains(
                    "FaceLandmarkList Annotation"
                );

            if (
                isLandmark
            )
            {
                renderer.enabled =
                    false;
            }
        }
    }

    // ============================================================
    // PATH
    // ============================================================

    string GetHierarchyPath(
        Transform t
    )
    {
        string path =
            t.name;

        Transform current =
            t.parent;

        while (
            current != null
        )
        {
            path =
                current.name +
                "/" +
                path;

            current =
                current.parent;
        }

        return path;
    }
}