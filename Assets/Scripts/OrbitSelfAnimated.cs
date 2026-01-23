using UnityEngine;

[ExecuteAlways]
public class OrbitCameraAnimated : MonoBehaviour
{
    [Header("Orbit Settings")]
    public Transform target;          // Точка, вокруг которой камера вращается
    public float distance = 10f;      // Радиус окружности
    [Range(0, 90)]
    public float rotationX = 35f;     // Наклон камеры по X
    public float startY = 25f;        // Начальный угол по Y (в градусах)
    public float endY = 45f;          // Конечный угол по Y (в градусах)

    [Header("Animation Settings")]
    public float speed = 1f;          // Скорость движения по дуге
    public bool playInEditMode = true; // Чтобы работало в редакторе

    private Camera cam;
    private float t;

    void OnEnable()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (target == null || cam == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying && !playInEditMode)
            return;
#endif

        // t двигается от 0 до 1 и обратно (туда-сюда)
        t += Time.deltaTime * speed;
        float pingpong = Mathf.PingPong(t, 1f);

        // Вычисляем текущий угол по оси Y
        float currentY = Mathf.Lerp(startY, endY, pingpong);

        // Поворот камеры
        Quaternion rot = Quaternion.Euler(rotationX, currentY, 0f);

        // Смещение камеры по орбите
        Vector3 offset = rot * new Vector3(0, 0, -distance);

        // Камера смотрит на цель
        cam.orthographic = true;
        cam.transform.position = target.position + offset;
        cam.transform.rotation = rot;
    }
}
