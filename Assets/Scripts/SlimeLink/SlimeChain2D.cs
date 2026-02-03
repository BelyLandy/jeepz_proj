using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SlimeChain2D : MonoBehaviour
{
    [Header("Chain Nodes (in order)")]
    public List<Transform> Nodes = new List<Transform>();

    [Header("Link setup")]
    [Tooltip("If set, newly created links copy ALL settings from this SlimeLink2D (except A/B and sprites).")]
    public SlimeLink2D SettingsFrom;

    [Tooltip("Material to apply to every generated link MeshRenderer.")]
    public Material LinkMaterial;

    [Header("Sorting (optional)")]
    public bool ForceSorting = false;
    public string SortingLayerName = "Default";
    public int SortingOrder = 0;
    public int SortingOrderStep = 0;

    [Header("Auto management")]
    public bool AutoRebuildInEditMode = true;

    [SerializeField] private List<SlimeLink2D> _links = new List<SlimeLink2D>();

    void OnEnable()
    {
        Sync();
    }

    void OnValidate()
    {
        if (AutoRebuildInEditMode) Sync();
    }

    void Update()
    {
        if (!Application.isPlaying && AutoRebuildInEditMode)
            Sync();
    }

    [ContextMenu("Sync Now")]
    public void Sync()
    {
        for (int i = Nodes.Count - 1; i >= 0; i--)
            if (Nodes[i] == null) Nodes.RemoveAt(i);

        int needed = Mathf.Max(0, Nodes.Count - 1);

        RefreshLinksFromChildren();

        if (_links.Count > needed)
        {
            for (int i = _links.Count - 1; i >= needed; i--)
                DestroyLink(_links[i]);
            _links.RemoveRange(needed, _links.Count - needed);
        }
        else if (_links.Count < needed)
        {
            for (int i = _links.Count; i < needed; i++)
                _links.Add(CreateLink(i));
        }

        for (int i = 0; i < needed; i++)
        {
            var link = _links[i];
            if (link == null) { _links[i] = CreateLink(i); link = _links[i]; }

            link.name = $"SlimeLink_{i}";

            link.A = Nodes[i];
            link.B = Nodes[i + 1];


            if (SettingsFrom != null)
                CopySettings(SettingsFrom, link);


            var mr = link.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (LinkMaterial != null) mr.sharedMaterial = LinkMaterial;

                if (ForceSorting)
                {
                    mr.sortingLayerName = SortingLayerName;
                    mr.sortingOrder = SortingOrder + i * SortingOrderStep;
                }
            }
        }
    }

    void RefreshLinksFromChildren()
    {
        _links.RemoveAll(x => x == null);
        var found = GetComponentsInChildren<SlimeLink2D>(true);

        var list = new List<SlimeLink2D>(found);
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (_links.Count == 0 && list.Count > 0)
            _links = list;
    }

    SlimeLink2D CreateLink(int index)
    {
        var go = new GameObject($"SlimeLink_{index}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(go, "Create Slime Link");
#endif

        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        var link = go.AddComponent<SlimeLink2D>();

        if (SettingsFrom != null)
            CopySettings(SettingsFrom, link);

        if (LinkMaterial != null)
            mr.sharedMaterial = LinkMaterial;

        if (ForceSorting)
        {
            mr.sortingLayerName = SortingLayerName;
            mr.sortingOrder = SortingOrder + index * SortingOrderStep;
        }

        return link;
    }

    void DestroyLink(SlimeLink2D link)
    {
        if (link == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.DestroyObjectImmediate(link.gameObject);
            return;
        }
#endif
        Destroy(link.gameObject);
    }

    static void CopySettings(SlimeLink2D src, SlimeLink2D dst)
    {
        if (src == null || dst == null) return;


        dst.AutoRadius = src.AutoRadius;
        dst.RadiusA = src.RadiusA;
        dst.RadiusB = src.RadiusB;


        dst.Segments = src.Segments;
        dst.MaxDistance = src.MaxDistance;
        dst.DistancePower = src.DistancePower;


        dst.RoundedCaps = src.RoundedCaps;
        dst.CapSegments = src.CapSegments;


        dst.MaxAttachAngleDeg = src.MaxAttachAngleDeg;
        dst.MinAttachAngleDeg = src.MinAttachAngleDeg;
        dst.HandleStrength = src.HandleStrength;
        dst.InwardPull = src.InwardPull;
        dst.NeckTightness = src.NeckTightness;


        dst.NeckCenter01 = src.NeckCenter01;
        dst.NeckSpread = src.NeckSpread;
        dst.ThicknessOverT = src.ThicknessOverT;


        dst.EnableMerge = src.EnableMerge;
        dst.MergeStartFactor = src.MergeStartFactor;
        dst.MergeEndFactor = src.MergeEndFactor;
        dst.MergeMaxAngleDeg = src.MergeMaxAngleDeg;
        dst.MergeInwardPull = src.MergeInwardPull;
        dst.MergeThicknessMultiplier = src.MergeThicknessMultiplier;


        dst.SimulateInEditMode = src.SimulateInEditMode;
        dst.Stiffness = src.Stiffness;
        dst.Damping = src.Damping;

        dst.DrawNeckCenterGizmo = false;

        dst.ASprite = null;
        dst.BSprite = null;
    }

#if UNITY_EDITOR
    [ContextMenu("Append Node Slot (null)")]
    void AppendNodeSlot()
    {
        Nodes.Add(null);
        Sync();
    }
#endif
}
