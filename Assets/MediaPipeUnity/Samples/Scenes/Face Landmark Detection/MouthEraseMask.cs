using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MouthEraseMask : MonoBehaviour
{
    [Header("References")]
    public Transform pointListAnnotation;
    public RawImage screenImage;
    public Material eraseMaterial;

    [Header("Mask Settings")]
    public float innerExpand = 1.35f;
    public float outerExpand = 1.75f;
    public float zOffset = -10f;

    [Header("Skin Color Sampling")]
    public bool enableSkinSampling = true;
    public float sampleInterval = 0.10f;
    public int sampleRadius = 4;
    public float colorLerpSpeed = 6f;

    [Header("Debug")]
    public Color sampledSkinColor =
        new Color(0.82f, 0.68f, 0.56f, 1f);

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;

    private float nextSampleTime = 0f;

    private Color currentColor =
        new Color(0.82f, 0.68f, 0.56f, 1f);

    // MediaPipe 外嘴唇轮廓
    private readonly int[] mouthOuter =
    {
        61,
        146,
        91,
        181,
        84,
        17,
        314,
        405,
        321,
        375,
        291,
        409,
        270,
        269,
        267,
        0,
        37,
        39,
        40,
        185
    };

    // 取嘴巴周围的皮肤，而不是直接取嘴唇
    // 左右脸颊 + 嘴周围相对稳定的位置
    private readonly int[] skinSampleLandmarks =
    {
        50,
        101,
        205,

        280,
        330,
        425
    };

    void Awake()
    {
        mesh = new Mesh();
        mesh.name = "Dynamic Mouth Erase Mask";

        MeshFilter meshFilter =
            GetComponent<MeshFilter>();

        meshFilter.mesh = mesh;

        meshRenderer =
            GetComponent<MeshRenderer>();

        if (eraseMaterial != null)
        {
            runtimeMaterial =
                new Material(eraseMaterial);

            meshRenderer.material =
                runtimeMaterial;

            ApplyColor(currentColor);
        }
        else
        {
            Debug.LogWarning(
                "MouthEraseMask: Erase Material 没有设置。"
            );
        }
    }

    void Update()
    {
        // 自动寻找整张脸的 468 个 landmark
        if (pointListAnnotation == null)
        {
            FindFacePointList();
            return;
        }

        if (pointListAnnotation.childCount < 468)
            return;

        // 自动寻找摄像头 RawImage
        if (screenImage == null)
        {
            FindScreenImage();
        }

        // 保持你现在已经成功的嘴巴遮罩
        UpdateMouthMask();

        // 定时采集肤色
        if (
            enableSkinSampling &&
            screenImage != null &&
            Time.time >= nextSampleTime
        )
        {
            SampleSkinColor();

            nextSampleTime =
                Time.time + sampleInterval;
        }

        // 颜色平滑变化，不要一帧一跳
        currentColor = Color.Lerp(
            currentColor,
            sampledSkinColor,
            Time.deltaTime * colorLerpSpeed
        );

        ApplyColor(currentColor);
    }

    void FindFacePointList()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (GameObject obj in allObjects)
        {
            if (obj.name != "Point List Annotation")
                continue;

            Transform t = obj.transform;

            if (t.parent == null)
                continue;

            // 只找完整 Face Landmark
            // 排除 Left Iris / Right Iris
            if (t.parent.name != "FaceLandmarkList Annotation")
                continue;

            if (t.childCount < 468)
                continue;

            pointListAnnotation = t;

            // 放入和 MediaPipe landmark 相同坐标系
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

            Debug.Log(
                "MouthEraseMask found FACE Point List Annotation."
            );

            return;
        }
    }

    void FindScreenImage()
    {
        RawImage[] images =
            FindObjectsByType<RawImage>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        // 优先寻找 Annotatable Screen 下面的 RawImage
        foreach (RawImage img in images)
        {
            Transform t = img.transform;

            while (t != null)
            {
                if (
                    t.name.Contains("Annotatable Screen") ||
                    t.name.Contains("AnnotatableScreen")
                )
                {
                    screenImage = img;

                    Debug.Log(
                        "MouthEraseMask found Screen Image: "
                        + img.name
                    );

                    return;
                }

                t = t.parent;
            }
        }

        // 如果找不到，就找名字带 Screen 的
        foreach (RawImage img in images)
        {
            if (
                img.name.ToLower().Contains("screen")
            )
            {
                screenImage = img;

                Debug.Log(
                    "MouthEraseMask found fallback Screen Image: "
                    + img.name
                );

                return;
            }
        }
    }

    void UpdateMouthMask()
    {
        int count = mouthOuter.Length;

        Vector3 center =
            Vector3.zero;

        // 计算嘴巴中心
        for (int i = 0; i < count; i++)
        {
            Vector3 p =
                pointListAnnotation
                .GetChild(mouthOuter[i])
                .localPosition;

            center += p;
        }

        center /= count;

        List<Vector3> vertices =
            new List<Vector3>();

        List<Color> colors =
            new List<Color>();

        List<int> triangles =
            new List<int>();

        // --------------------
        // 中心点
        // --------------------

        Vector3 centerVertex =
            center;

        centerVertex.z += zOffset;

        vertices.Add(centerVertex);

        colors.Add(
            new Color(
                1f,
                1f,
                1f,
                1f
            )
        );

        // --------------------
        // 内圈：完全覆盖
        // --------------------

        for (int i = 0; i < count; i++)
        {
            Vector3 p =
                pointListAnnotation
                .GetChild(mouthOuter[i])
                .localPosition;

            p.x =
                center.x +
                (p.x - center.x) *
                innerExpand;

            p.y =
                center.y +
                (p.y - center.y) *
                innerExpand;

            p.z += zOffset;

            vertices.Add(p);

            colors.Add(
                new Color(
                    1f,
                    1f,
                    1f,
                    1f
                )
            );
        }

        // --------------------
        // 外圈：完全透明
        // --------------------

        for (int i = 0; i < count; i++)
        {
            Vector3 p =
                pointListAnnotation
                .GetChild(mouthOuter[i])
                .localPosition;

            p.x =
                center.x +
                (p.x - center.x) *
                outerExpand;

            p.y =
                center.y +
                (p.y - center.y) *
                outerExpand;

            p.z += zOffset;

            vertices.Add(p);

            colors.Add(
                new Color(
                    1f,
                    1f,
                    1f,
                    0f
                )
            );
        }

        // 中心 → 内圈
        for (int i = 0; i < count; i++)
        {
            int next =
                (i + 1) % count;

            int innerA =
                1 + i;

            int innerB =
                1 + next;

            triangles.Add(0);
            triangles.Add(innerA);
            triangles.Add(innerB);
        }

        // 内圈 → 外圈（羽化）
        int outerStart =
            1 + count;

        for (int i = 0; i < count; i++)
        {
            int next =
                (i + 1) % count;

            int innerA =
                1 + i;

            int innerB =
                1 + next;

            int outerA =
                outerStart + i;

            int outerB =
                outerStart + next;

            triangles.Add(innerA);
            triangles.Add(outerA);
            triangles.Add(outerB);

            triangles.Add(innerA);
            triangles.Add(outerB);
            triangles.Add(innerB);
        }

        mesh.Clear();

        mesh.SetVertices(vertices);
        mesh.SetColors(colors);

        mesh.SetTriangles(
            triangles,
            0
        );

        mesh.RecalculateBounds();
    }

    void SampleSkinColor()
    {
        if (screenImage == null)
            return;

        Texture texture =
            screenImage.texture;

        if (texture == null)
            return;

        Canvas canvas =
            screenImage.canvas;

        Camera uiCamera = null;

        if (
            canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay
        )
        {
            uiCamera =
                canvas.worldCamera;
        }

        float totalR = 0f;
        float totalG = 0f;
        float totalB = 0f;

        int validSamples = 0;

        foreach (
            int landmarkIndex
            in skinSampleLandmarks
        )
        {
            if (
                landmarkIndex >=
                pointListAnnotation.childCount
            )
                continue;

            Transform landmark =
                pointListAnnotation
                .GetChild(landmarkIndex);

            // Landmark 世界位置
            // 转换成屏幕坐标
            Vector2 screenPoint =
                RectTransformUtility
                .WorldToScreenPoint(
                    uiCamera,
                    landmark.position
                );

            Vector2 localPoint;

            bool success =
                RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    screenImage.rectTransform,
                    screenPoint,
                    uiCamera,
                    out localPoint
                );

            if (!success)
                continue;

            Rect rect =
                screenImage.rectTransform.rect;

            // RawImage 内部的 0~1 UV
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

            // MediaPipe Sample 会利用 uvRect
            // 处理摄像头镜像/翻转
            Rect uvRect =
                screenImage.uvRect;

            u =
                uvRect.x +
                u * uvRect.width;

            v =
                uvRect.y +
                v * uvRect.height;

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            // --------------------
            // WebCamTexture
            // --------------------

            if (texture is WebCamTexture webcam)
            {
                if (
                    !webcam.isPlaying ||
                    webcam.width < 32 ||
                    webcam.height < 32
                )
                    continue;

                int centerX =
                    Mathf.RoundToInt(
                        u *
                        (webcam.width - 1)
                    );

                int centerY =
                    Mathf.RoundToInt(
                        v *
                        (webcam.height - 1)
                    );

                SampleWebCamPatch(
                    webcam,
                    centerX,
                    centerY,
                    ref totalR,
                    ref totalG,
                    ref totalB,
                    ref validSamples
                );
            }

            // --------------------
            // Texture2D fallback
            // --------------------

            else if (
                texture is Texture2D texture2D &&
                texture2D.isReadable
            )
            {
                int centerX =
                    Mathf.RoundToInt(
                        u *
                        (texture2D.width - 1)
                    );

                int centerY =
                    Mathf.RoundToInt(
                        v *
                        (texture2D.height - 1)
                    );

                SampleTexture2DPatch(
                    texture2D,
                    centerX,
                    centerY,
                    ref totalR,
                    ref totalG,
                    ref totalB,
                    ref validSamples
                );
            }
        }

        if (validSamples == 0)
            return;

        sampledSkinColor =
            new Color(
                totalR / validSamples,
                totalG / validSamples,
                totalB / validSamples,
                1f
            );
    }

    void SampleWebCamPatch(
        WebCamTexture webcam,
        int centerX,
        int centerY,
        ref float totalR,
        ref float totalG,
        ref float totalB,
        ref int validSamples
    )
    {
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
                        centerX + x,
                        0,
                        webcam.width - 1
                    );

                int py =
                    Mathf.Clamp(
                        centerY + y,
                        0,
                        webcam.height - 1
                    );

                Color c =
                    webcam.GetPixel(
                        px,
                        py
                    );

                AddSkinSample(
                    c,
                    ref totalR,
                    ref totalG,
                    ref totalB,
                    ref validSamples
                );
            }
        }
    }

    void SampleTexture2DPatch(
        Texture2D texture,
        int centerX,
        int centerY,
        ref float totalR,
        ref float totalG,
        ref float totalB,
        ref int validSamples
    )
    {
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
                        centerX + x,
                        0,
                        texture.width - 1
                    );

                int py =
                    Mathf.Clamp(
                        centerY + y,
                        0,
                        texture.height - 1
                    );

                Color c =
                    texture.GetPixel(
                        px,
                        py
                    );

                AddSkinSample(
                    c,
                    ref totalR,
                    ref totalG,
                    ref totalB,
                    ref validSamples
                );
            }
        }
    }

    void AddSkinSample(
        Color c,
        ref float totalR,
        ref float totalG,
        ref float totalB,
        ref int validSamples
    )
    {
        // 排除过暗和过曝区域
        float brightness =
            (c.r + c.g + c.b) / 3f;

        if (
            brightness < 0.12f ||
            brightness > 0.95f
        )
            return;

        // 排除过于鲜艳的颜色
        Color.RGBToHSV(
            c,
            out float h,
            out float s,
            out float v
        );

        if (s > 0.75f)
            return;

        totalR += c.r;
        totalG += c.g;
        totalB += c.b;

        validSamples++;
    }

    void ApplyColor(Color c)
    {
        if (runtimeMaterial == null)
            return;

        if (
            runtimeMaterial.HasProperty("_Color")
        )
        {
            runtimeMaterial.SetColor(
                "_Color",
                c
            );
        }

        if (
            runtimeMaterial.HasProperty("_BaseColor")
        )
        {
            runtimeMaterial.SetColor(
                "_BaseColor",
                c
            );
        }
    }
}