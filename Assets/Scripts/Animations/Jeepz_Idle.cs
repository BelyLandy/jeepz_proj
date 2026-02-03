using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
//[AddComponentMenu("Audio/Beat-Driven Rig Animator")]
public class Jeepz_Idle : MonoBehaviour
{
    [SerializeField] private int pixelsPerUnit = 32;

    [Header("Beat source")]
    [Tooltip("Перетащи сюда BeatConductor из сцены")]
    [SerializeField] private BeatConductor conductor;

    [Header("Rig root")]
    [Tooltip("Корневой трансформ рига. По умолчанию — этот объект.")]
    public Transform rigRoot;

    [Header("Looping")]
    [Tooltip("Сколько ударов длится один цикл этой анимации (четвертей).")]
    public int beatsPerLoop = 1;
    [Tooltip("Смещение в ударах относительно старта трека (можно дробное).")]
    public float beatOffset = 0f;

    struct Curve2
    {
        public Vector2 k0, k1, k2;
        public Curve2(Vector2 a, Vector2 b, Vector2 c) { k0 = a; k1 = b; k2 = c; }
        public Vector2 Evaluate01(float u)
        {
            u = Mathf.Repeat(u, 1f);
            if (u <= 0.5f)
            {
                float t = u / 0.5f;
                return Vector2.LerpUnclamped(k0, k1, t);
            }
            else
            {
                float t = (u - 0.5f) / 0.5f;
                return Vector2.LerpUnclamped(k1, k2, t);
            }
        }
    }

    class Part
    {
        public string path;
        public Transform t;
        public Curve2 curve;
    }

    readonly List<Part> parts = new List<Part>();

    long beatCounter = 0;
    float lastBeatPhase = 0f;

    bool subscribed;

    void Reset()
    {
        rigRoot = transform;
    }

    void Awake()
    {
        if (!rigRoot) rigRoot = transform;

        void Add(string path, Vector2 k0, Vector2 k1, Vector2 k2)
        {
            var tr = rigRoot ? rigRoot.Find(path) : null;
            if (!tr) Debug.LogWarning($"[BeatRig] Не найден узел: {path}", this);
            parts.Add(new Part { path = path, t = tr, curve = new Curve2(k0, k1, k2) });
        }

        Add("Parts/Body", new Vector2(0.15625f, 0.28125f), new Vector2(0.09375f, 0.21875f), new Vector2(0.15625f, 0.28125f));
        Add("Parts/Head", new Vector2(0.28125f, 0.625f), new Vector2(0.3125f, 0.59375f), new Vector2(0.28125f, 0.625f));
        Add("Parts/Shoulder_Left", new Vector2(-0.15625f, 0.4375f), new Vector2(-0.15625f, 0.375f), new Vector2(-0.15625f, 0.4375f));
        Add("Parts/Shoulder_Right", new Vector2(0.28125f, 0.3125f), new Vector2(0.28125f, 0.21875f), new Vector2(0.28125f, 0.3125f));
        Add("Parts/Elbow_Left", new Vector2(-0.46875f, 0.21875f), new Vector2(-0.5f, 0.125f), new Vector2(-0.46875f, 0.21875f));
        Add("Parts/Elbow_Right", new Vector2(0.375f, -0.03125f), new Vector2(0.3125f, -0.03125f), new Vector2(0.375f, -0.03125f));
        Add("Parts/Ring", new Vector2(0.125f, 0.34375f), new Vector2(0.09375f, 0.3125f), new Vector2(0.125f, 0.34375f));
        Add("Parts/Join_1", new Vector2(-0.40625f, 0), new Vector2(-0.4375f, -0.03125f), new Vector2(-0.40625f, 0));
        Add("Parts/Hand_Left", new Vector2(-0.25f, -0.21875f), new Vector2(-0.25f, -0.28125f), new Vector2(-0.25f, -0.21875f));
        Add("Parts/Join_4", new Vector2(0.5f, -0.125f), new Vector2(0.4375f, -0.125f), new Vector2(0.5f, -0.125f));
        Add("Parts/Hand_Right", new Vector2(0.71875f, -0.125f), new Vector2(0.65625f, -0.1875f), new Vector2(0.71875f, -0.125f));
        Add("Parts/Hip_Left", new Vector2(-0.125f, -0.21875f), new Vector2(-0.125f, -0.25f), new Vector2(-0.125f, -0.21875f));
        Add("Parts/Hip_Right", new Vector2(-0.125f, -0.21875f), new Vector2(-0.15625f, -0.25f), new Vector2(-0.125f, -0.21875f));
        Add("Parts/Knee_Right", new Vector2(0.5f, -0.40625f), new Vector2(0.53125f, -0.4375f), new Vector2(0.5f, -0.40625f));
        Add("Parts/Knee_Left", new Vector2(-0.1875f, -0.5625f), new Vector2(-0.125f, -0.59375f), new Vector2(-0.1875f, -0.5625f));
        Add("Parts/Join_3", new Vector2(-0.3125f, -0.625f), new Vector2(-0.34375f, -0.65625f), new Vector2(-0.3125f, -0.625f));
        Add("Parts/Join_2", new Vector2(0.5f, -0.5625f), new Vector2(0.5f, -0.625f), new Vector2(0.5f, -0.5625f));
        //Add("Parts/EyeBeam", new Vector2(0.456f, 0.462f), new Vector2(0.499f, 0.4424f), new Vector2(0.456f, 0.462f));

    }

    void OnEnable() { TrySubscribe(); }
    void OnDisable() { Unsubscribe(); }

    void OnValidate()
    {
        if (!rigRoot) rigRoot = transform;
        if (enabled && gameObject.activeInHierarchy)
        {
            Unsubscribe();
            TrySubscribe();
        }
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (!conductor)
        {
            //Debug.LogWarning("[BeatRig] Не задан BeatConductor в инспекторе.", this);
            return;
        }
        conductor.OnBeat += OnBeat;
        conductor.OnBeatPhase += OnBeatPhase;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed) return;
        if (conductor)
        {
            conductor.OnBeat -= OnBeat;
            conductor.OnBeatPhase -= OnBeatPhase;
        }
        subscribed = false;
    }

    void OnBeat() => beatCounter++;

    void OnBeatPhase(float phase)
    {
        lastBeatPhase = Mathf.Clamp01(phase);
        Animate();
    }

    void Animate()
    {
        if (parts.Count == 0 || beatsPerLoop <= 0) return;

        double totalBeats = beatCounter + lastBeatPhase + beatOffset;
        double loopPhase = totalBeats / beatsPerLoop;
        loopPhase -= Math.Floor(loopPhase);
        float u = (float)loopPhase; // 0..1

        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (!p.t) continue;

            float ppu = (float)pixelsPerUnit;
            Vector2 pos = p.curve.Evaluate01(u);
            pos.x = Mathf.Round(pos.x * ppu) / ppu;
            pos.y = Mathf.Round(pos.y * ppu) / ppu;
            var lp = p.t.localPosition;
            lp.x = pos.x;
            lp.y = pos.y;
            lp.z = 0f;
            p.t.localPosition = lp;
        }
    }
}
