using UnityEngine;
using System.Collections;

public class FaceOnPressSmooth : MonoBehaviour
{
    [Header("Кого вращать (обычно корень Jeepz)")]
    public Transform rotateRoot;

    [Header("Параметры поворота")]
    public float turnDuration = 0.12f; // длительность плавного доворота
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float deadzone = 0.01f;

    [Header("Углы")]
    public float rightYaw = 0f;
    public float leftYaw = 180f;

    int _facing = 0; // -1=влево, +1=вправо, 0=не выбран
    Coroutine _rotJob;

    void Reset() => rotateRoot = transform;

    void Update()
    {
        float inputX = Input.GetAxisRaw("Horizontal");

        if (inputX > deadzone && _facing != +1) StartFlip(+1);
        else if (inputX < -deadzone && _facing != -1) StartFlip(-1);
    }

    void StartFlip(int dir)
    {
        _facing = dir;
        float targetYaw = dir > 0 ? rightYaw : leftYaw;

        if (_rotJob != null) StopCoroutine(_rotJob);
        _rotJob = StartCoroutine(RotateToYaw(targetYaw));
    }

    IEnumerator RotateToYaw(float targetYaw)
    {
        if (rotateRoot == null) yield break;

        Quaternion start = rotateRoot.rotation;
        Quaternion target = Quaternion.Euler(0f, targetYaw, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += (turnDuration <= 0f ? 1f : Time.deltaTime / turnDuration);
            float k = ease.Evaluate(Mathf.Clamp01(t));
            rotateRoot.rotation = Quaternion.Slerp(start, target, k);
            yield return null;
        }

        rotateRoot.rotation = target;
        _rotJob = null;
    }

    public void FaceLeft()  => StartFlip(-1);
    public void FaceRight() => StartFlip(+1);
}
