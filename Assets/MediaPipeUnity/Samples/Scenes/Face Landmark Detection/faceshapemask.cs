using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FaceShapeMask : MonoBehaviour
{
    [Header("References")]
    public Transform pointListAnnotation;
    public RawImage screenImage;
    public Material maskMaterial;

    [Header("Mask Settings")]
    public float zOffset = -10f;

    // 稍微向外扩张，避免转头时边缘露出真实脸
    public float expandX = 1.08f;
    public float expandY = 1.04f;

    [Header("Skin Color")]
    public float sampleInterval = 0.10f;
    public int sampleRadius = 5;
    public float colorLerpSpeed = 6f;

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;

    private float nextSampleTime = 0f;

    private Color currentColor =
        new Color(0.82f, 0.68f, 0.56f, 1f);

    private Color targetColor =
        new Color(0.82f, 0.68f, 0.56f, 1f);

    // MediaPipe Face Oval
    private readonly int[] faceOval =
    {
        10, 338, 297, 332, 284, 251, 389, 356, 454,
        323, 361, 288, 397, 365, 379, 378, 400, 377,
        152, 148, 176, 149, 150, 136, 172, 58, 132,
        93, 234, 127, 162, 21, 54, 103, 67, 109
    };

    // 尽量选择左右脸颊区域，避开眼睛、嘴巴、头发
    private readonly int[] skinSampleLandmarks =
    {
        50, 101, 118,
        280, 330, 347
    };

    void Awake()
    {
        mesh = new Mesh();
        mesh.name = "Dynamic Face Mask";

        GetComponent<MeshFilter>().mesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>();

        if (maskMaterial != null)
        {
            runtimeMaterial = new Material(maskMaterial);
            meshRenderer.material = runtimeMaterial;
        }
        else
        {
            Debug.LogWarning("FaceShapeMask: Mask Material 没有设置。");
        }
    }

    void Update()
    {
        if (pointListAnnotation == null)
        {
            FindPointListAnnotation();
            return;
        }

        if (pointListAnnotation.childCount < 468)
            return;

        UpdateFaceMesh();

        if (Time.time >= nextSampleTime)
        {
            SampleSkinColor();

            nextSampleTime =
                Time.time + sampleInterval;
        }

        currentColor = Color.Lerp(
            currentColor,
            targetColor,
            Time.deltaTime * colorLerpSpeed
        );

        ApplyColor(currentColor);
    }

    void FindPointListAnnotation()
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

            // 只找整张脸，排除左右虹膜
            if (t.parent.name != "FaceLandmarkList Annotation")
                continue;

            if (t.childCount < 468)
                continue;

            pointListAnnotation = t;

            transform.SetParent(
                pointListAnnotation,
                false
            );

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            Debug.Log(
                "FaceShapeMask 找到整脸 landmarks: "
                + t.childCount
            );

            return;
        }
    }

    void UpdateFaceMesh()
    {
        List<Vector3> vertices =
            new List<Vector3>();

        List<int> triangles =
            new List<int>();

        Vector3 center = Vector3.zero;

        // 先取得原始外轮廓
        for (int i = 0; i < faceOval.Length; i++)
        {
            Vector3 p =
                pointListAnnotation
                .GetChild(faceOval[i])
                .localPosition;

            center += p;
        }

        center /= faceOval.Length;

        // 再根据中心向外稍微扩张
        for (int i = 0; i < faceOval.Length; i++)
        {
            Vector3 p =
                pointListAnnotation
                .GetChild(faceOval[i])
                .localPosition;

            p.x =
                center.x +
                (p.x - center.x) * expandX;

            p.y =
                center.y +
                (p.y - center.y) * expandY;

            p.z += zOffset;

            vertices.Add(p);
        }

        Vector3 meshCenter = center;
        meshCenter.z += zOffset;

        int centerIndex = vertices.Count;

        vertices.Add(meshCenter);

        for (int i = 0; i < faceOval.Length; i++)
        {
            int next =
                (i + 1) % faceOval.Length;

            // 双面
            triangles.Add(centerIndex);
            triangles.Add(i);
            triangles.Add(next);

            triangles.Add(centerIndex);
            triangles.Add(next);
            triangles.Add(i);
        }

        mesh.Clear();

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void SampleSkinColor()
    {
        if (screenImage == null)
            return;

        if (screenImage.texture == null)
            return;

        WebCamTexture webcam =
            screenImage.texture as WebCamTexture;

        if (webcam == null)
            return;

        if (!webcam.isPlaying)
            return;

        if (webcam.width < 32 ||
            webcam.height < 32)
            return;

        Color32[] pixels =
            webcam.GetPixels32();

        if (pixels == null ||
            pixels.Length == 0)
            return;

        RectTransform imageRect =
            screenImage.rectTransform;

        Canvas canvas =
            screenImage.canvas;

        Camera uiCamera = null;

        if (canvas != null &&
            canvas.renderMode !=
            RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        float totalR = 0f;
        float totalG = 0f;
        float totalB = 0f;

        int validCount = 0;

        foreach (int index in skinSampleLandmarks)
        {
            if (index >=
                pointListAnnotation.childCount)
                continue;

            Transform landmark =
                pointListAnnotation.GetChild(index);

            // Landmark 世界位置 → 屏幕位置
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    uiCamera,
                    landmark.position
                );

            Vector2 localPoint;

            bool inside =
                RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    imageRect,
                    screenPoint,
                    uiCamera,
                    out localPoint
                );

            if (!inside)
                continue;

            Rect rect = imageRect.rect;

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

            // 考虑 RawImage 的 UV 设置
            Rect uv = screenImage.uvRect;

            u = Mathf.Lerp(
                uv.xMin,
                uv.xMax,
                u
            );

            v = Mathf.Lerp(
                uv.yMin,
                uv.yMax,
                v
            );

            int px = Mathf.Clamp(
                Mathf.RoundToInt(
                    u * (webcam.width - 1)
                ),
                0,
                webcam.width - 1
            );

            int py = Mathf.Clamp(
                Mathf.RoundToInt(
                    v * (webcam.height - 1)
                ),
                0,
                webcam.height - 1
            );

            // 每个 landmark 周围采一小块区域
            for (int y = -sampleRadius;
                 y <= sampleRadius;
                 y++)
            {
                for (int x = -sampleRadius;
                     x <= sampleRadius;
                     x++)
                {
                    int sx = Mathf.Clamp(
                        px + x,
                        0,
                        webcam.width - 1
                    );

                    int sy = Mathf.Clamp(
                        py + y,
                        0,
                        webcam.height - 1
                    );

                    Color32 c =
                        pixels[
                            sy * webcam.width + sx
                        ];

                    float r = c.r / 255f;
                    float g = c.g / 255f;
                    float b = c.b / 255f;

                    float brightness =
                        (r + g + b) / 3f;

                    // 排除极暗和过曝
                    if (brightness < 0.16f ||
                        brightness > 0.93f)
                        continue;

                    totalR += r;
                    totalG += g;
                    totalB += b;

                    validCount++;
                }
            }
        }

        if (validCount == 0)
            return;

        targetColor = new Color(
            totalR / validCount,
            totalG / validCount,
            totalB / validCount,
            1f
        );
    }

    void ApplyColor(Color color)
    {
        if (runtimeMaterial == null)
            return;

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor(
                "_Color",
                color
            );
        }

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor(
                "_BaseColor",
                color
            );
        }
    }
}