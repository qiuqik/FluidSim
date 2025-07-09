using TMPro;
using UnityEngine;
using UnityEngine.UI;  // ���� UI ��

public class FPSDisplay : MonoBehaviour
{
    // �� Inspector ������ UI Ԫ��
    public TextMeshProUGUI fpsText;
    private float deltaTime = 0.0f;

    void Update()
    {
        // �ۻ�ʱ�䣬ÿ֡����
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // ���㵱ǰ֡�� (ÿ���֡��)
        float fps = 1.0f / deltaTime;

        // ʵʱ���� UI �ϵ��ı�
        fpsText.text = "FPS: " + fps.ToString("F2");  // ��ʾ֡�ʣ���������Ϊ����
    }
}
