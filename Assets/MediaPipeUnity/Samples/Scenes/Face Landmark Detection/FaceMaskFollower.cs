using UnityEngine;

public class FaceMaskFollower : MonoBehaviour
{
    public Transform pointListAnnotation;

    public float smoothSpeed = 12f;

    public float scaleXMultiplier = 0.9f;
    public float scaleYMultiplier = 1.05f;

    public Vector3 positionOffset = Vector3.zero;

    private bool parented = false;

    void Update()
    {
        if (pointListAnnotation == null)
        {
            FindPointListAnnotation();
            return;
        }

        // 第一次找到后，让面罩进入和绿色关键点完全一样的坐标系
        if (!parented)
        {
            transform.SetParent(pointListAnnotation, false);
            parented = true;
        }

        if (pointListAnnotation.childCount == 0)
            return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (Transform point in pointListAnnotation)
        {
            // 排除 FaceMask 自己
            if (point == transform)
                continue;

            Vector3 p = point.localPosition;

            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);

            center += p;
            count++;
        }

        if (count == 0)
            return;

        center /= count;

        Vector3 targetPosition =
            center + positionOffset;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPosition,
            Time.deltaTime * smoothSpeed
        );

        float width = maxX - minX;
        float height = maxY - minY;

        Vector3 targetScale = new Vector3(
            width * scaleXMultiplier,
            height * scaleYMultiplier,
            20f
        );

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.deltaTime * smoothSpeed
        );
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
            if (obj.name == "Point List Annotation")
            {
                pointListAnnotation = obj.transform;
                Debug.Log("Found Point List Annotation automatically.");
                return;
            }
        }
    }
}