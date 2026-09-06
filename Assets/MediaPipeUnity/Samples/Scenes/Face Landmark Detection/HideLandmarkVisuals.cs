using UnityEngine;

public class HideLandmarkVisuals : MonoBehaviour
{
    [Header("Settings")]
    public bool hideIris = true;

    private Transform pointListAnnotation;
    private bool hasHidden = false;

    void Update()
    {
        if (hasHidden)
            return;

        if (pointListAnnotation == null)
            FindFacePointList();

        if (pointListAnnotation == null)
            return;

        HideVisuals();
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

            if (t.parent.name != "FaceLandmarkList Annotation")
                continue;

            if (t.childCount < 468)
                continue;

            pointListAnnotation = t;
            return;
        }
    }

    void HideVisuals()
    {
        Transform root = pointListAnnotation.parent;

        if (
            hideIris &&
            root.parent != null &&
            root.parent.name.Contains(
                "FaceLandmarkListWithIris Annotation"
            )
        )
        {
            root = root.parent;
        }

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            // 不隐藏我们自己做的 AR 遮罩
            if (r.GetComponent<MidFaceEraseMask>() != null)
                continue;

            if (r.GetComponent<MouthEraseMask>() != null)
                continue;

            if (r.GetComponent<FaceShapeMask>() != null)
                continue;

            r.enabled = false;
        }

        hasHidden = true;

        Debug.Log(
            "Landmark visuals hidden, custom masks preserved."
        );
    }
}