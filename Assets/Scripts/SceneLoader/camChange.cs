using UnityEngine;

public class CameraOrbitControl : MonoBehaviour
{
    public Transform pivot;          // ��ת�����ĵ�
    public float rotationSpeed = 0.2f;  // ��ת�ٶ�
    public float zoomSpeed = 0.02f;     // �����ٶ�
    public float minZoom = 2f;          // �������
    private float maxZoom;              // ���������루��̬��

    private float distance; // �������Ŀ���ĵ�ǰ����
    private float initialDistance; // ��¼�������ʼ����

    void Start()
    {
        initialDistance = Vector3.Distance(transform.position, pivot.position);
        distance = initialDistance;
        maxZoom = initialDistance * 1.5f; // ����������ڳ�ʼ���룬��ֹ�޷��ص�ԭ��
    }

    void Update()
    {
        if (Input.touchCount == 1)  // ��ָ��ת
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                float rotX = touch.deltaPosition.x * rotationSpeed;
                float rotY = -touch.deltaPosition.y * rotationSpeed;

                // �������Χ�� pivot ��ת
                transform.RotateAround(pivot.position, Vector3.up, rotX);
                transform.RotateAround(pivot.position, transform.right, rotY);
            }
        }
        else if (Input.touchCount == 2)  // ˫ָ����
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            float prevDistance = (touch1.position - touch1.deltaPosition - (touch2.position - touch2.deltaPosition)).magnitude;
            float currentDistance = (touch1.position - touch2.position).magnitude;
            float deltaDistance = currentDistance - prevDistance;

            // �����µ�����Ŀ�����
            float targetDistance = distance - (deltaDistance * zoomSpeed * distance);
            distance = Mathf.Clamp(targetDistance, minZoom, maxZoom); // �������ŷ�Χ

            // ƽ�����������λ��
            transform.position = Vector3.Lerp(transform.position, pivot.position - transform.forward * distance, 0.15f);
        }
    }
}
