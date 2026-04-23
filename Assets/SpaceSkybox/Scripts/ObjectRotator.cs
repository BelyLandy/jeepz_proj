using UnityEngine;


public class ObjectRotator : MonoBehaviour
{
    [SerializeField]
    protected Vector3 EulerAngles;
    protected void Update()
    {
        transform.Rotate(EulerAngles * Time.deltaTime);
    }
}

