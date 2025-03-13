using UnityEngine;

public class CameraOrbitControl : MonoBehaviour
{
    public Transform pivot;          // 旋转的中心点
    public float rotationSpeed = 0.2f;  // 旋转速度
    public float zoomSpeed = 0.02f;     // 缩放速度
    public float minZoom = 2f;          // 最近距离
    private float maxZoom;              // 计算最大距离（动态）

    private float distance; // 摄像机到目标点的当前距离
    private float initialDistance; // 记录摄像机初始距离

    void Start()
    {
        initialDistance = Vector3.Distance(transform.position, pivot.position);
        distance = initialDistance;
        maxZoom = initialDistance * 1.5f; // 让最大距离大于初始距离，防止无法回到原点
    }

    void Update()
    {
        if (Input.touchCount == 1)  // 单指旋转
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                float rotX = touch.deltaPosition.x * rotationSpeed;
                float rotY = -touch.deltaPosition.y * rotationSpeed;

                // 让摄像机围绕 pivot 旋转
                transform.RotateAround(pivot.position, Vector3.up, rotX);
                transform.RotateAround(pivot.position, transform.right, rotY);
            }
        }
        else if (Input.touchCount == 2)  // 双指缩放
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            float prevDistance = (touch1.position - touch1.deltaPosition - (touch2.position - touch2.deltaPosition)).magnitude;
            float currentDistance = (touch1.position - touch2.position).magnitude;
            float deltaDistance = currentDistance - prevDistance;

            // 计算新的缩放目标距离
            float targetDistance = distance - (deltaDistance * zoomSpeed * distance);
            distance = Mathf.Clamp(targetDistance, minZoom, maxZoom); // 限制缩放范围

            // 平滑缩放摄像机位置
            transform.position = Vector3.Lerp(transform.position, pivot.position - transform.forward * distance, 0.15f);
        }
    }
}
