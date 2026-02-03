using UnityEngine;

public class ClothArcShooter : MonoBehaviour
{
    public VerletCloth2D cloth;
    public Camera cam;
    public ClothArcProjectile projectilePrefab;

    [Header("Spawn from outside screen")]
    [Range(0.01f, 0.5f)] public float viewportPadding = 0.12f;

    public enum SpawnEdge { BottomLeftCorner, BottomRightCorner, Left, Right, Bottom, Top, Random }

    public SpawnEdge spawnEdge = SpawnEdge.BottomLeftCorner;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (cloth == null || cam == null || projectilePrefab == null) return;

        if (Input.GetMouseButtonDown(1))
        {
            float clothZ = cloth.transform.position.z;
            Vector3 target3 = ScreenToWorldOnZPlane(Input.mousePosition, clothZ);
            Vector2 targetXY = new Vector2(target3.x, target3.y);

            Vector3 spawn3 = GetOffscreenSpawnOnZ(clothZ);
            Vector2 spawnXY = new Vector2(spawn3.x, spawn3.y);

            var proj = Instantiate(projectilePrefab, spawn3, Quaternion.identity);
            proj.Init(cloth, spawnXY, targetXY, clothZ);
        }
    }

    private Vector3 ScreenToWorldOnZPlane(Vector3 screenPos, float zPlane)
    {
        Ray r = cam.ScreenPointToRay(screenPos);
        Plane p = new Plane(Vector3.forward, new Vector3(0, 0, zPlane));
        if (p.Raycast(r, out float enter))
            return r.GetPoint(enter);

        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z - zPlane)));
        w.z = zPlane;
        return w;
    }

    private Vector3 GetOffscreenSpawnOnZ(float clothZ)
    {
        float depthToCloth = Vector3.Dot((new Vector3(0, 0, clothZ) - cam.transform.position), cam.transform.forward);
        depthToCloth = Mathf.Max(0.1f, depthToCloth);

        float pad = viewportPadding;
        float vx = 0f, vy = 0f;

        SpawnEdge edge = spawnEdge;
        if (edge == SpawnEdge.Random) edge = (SpawnEdge)Random.Range(0, 6);

        switch (edge)
        {
            case SpawnEdge.BottomLeftCorner:
                vx = -pad;
                vy = -pad;
                break;

            case SpawnEdge.BottomRightCorner:
                vx = 1f + pad;
                vy = -pad;
                break;

            case SpawnEdge.Left:
                vx = -pad;
                vy = Random.Range(0f, 1f);
                break;

            case SpawnEdge.Right:
                vx = 1f + pad;
                vy = Random.Range(0f, 1f);
                break;

            case SpawnEdge.Bottom:
                vx = Random.Range(0f, 1f);
                vy = -pad;
                break;

            default: // Top
                vx = Random.Range(0f, 1f);
                vy = 1f + pad;
                break;
        }


        Vector3 w = cam.ViewportToWorldPoint(new Vector3(vx, vy, depthToCloth));
        w.z = clothZ;
        return w;
    }
}
