using TMPro;
using UnityEngine;
using UnityEngine.UI;  // 引入 UI 库

public class FPSDisplay : MonoBehaviour
{
    // 在 Inspector 中连接 UI 元素
    public TextMeshProUGUI fpsText;
    private float deltaTime = 0.0f;

    void Update()
    {
        // 累积时间，每帧计算
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // 计算当前帧率 (每秒的帧数)
        float fps = 1.0f / deltaTime;

        // 实时更新 UI 上的文本
        fpsText.text = "FPS: " + fps.ToString("F2");  // 显示帧率，四舍五入为整数
    }
}
