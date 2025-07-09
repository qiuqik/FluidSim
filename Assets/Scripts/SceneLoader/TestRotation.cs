using UnityEngine;

public class TestRotation : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(Vector3.up, 50f * Time.deltaTime, Space.World);
    }
}
