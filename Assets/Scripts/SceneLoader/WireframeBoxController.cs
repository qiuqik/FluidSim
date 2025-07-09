
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Mathematics;
//using UnityEngine.UIElements;

public class WireframeBoxController : MonoBehaviour
{
    public LineRenderer rectRenderer;  // 长方体线框
    public LineRenderer cubeRenderer;  // 正方体线框
    public Slider lengthSlider, widthSlider, heightSlider, cubeSizeSlider, rotateSlider, IterationSlider;
    public TextMeshProUGUI lengthText, widthText, heightText, cubeSizeText, particleNumsText, IterationText;
    public float rotationSpeed = 0.001f;

    private void Start()
    {
        // 初始化 LineRenderer（长方体 = 绿色，正方体 = 红色）
        SetupLineRenderer(rectRenderer, Color.green);
        SetupLineRenderer(cubeRenderer, Color.red);

        // 监听 Slider 变化
        lengthSlider.onValueChanged.AddListener(delegate { UpdateWireframes(); });
        widthSlider.onValueChanged.AddListener(delegate { UpdateWireframes(); });
        heightSlider.onValueChanged.AddListener(delegate { UpdateWireframes(); });
        cubeSizeSlider.onValueChanged.AddListener(delegate { UpdateWireframes(); });
        rotateSlider.onValueChanged.AddListener(delegate { UpdateWireframes(); });
        IterationSlider.onValueChanged.AddListener(delegate { UpdateWireframes(); });

        // 初始更新线框
        InitWireframes();


    }
    void InitWireframes()
    {
        if (PlayerPrefs.HasKey("BoxX"))
        {
            lengthSlider.value = PlayerPrefs.GetFloat("BoxX");
            widthSlider.value = PlayerPrefs.GetFloat("BoxY");
            heightSlider.value = PlayerPrefs.GetFloat("BoxZ");
            cubeSizeSlider.value = PlayerPrefs.GetFloat("cubeSize");
        }

        IterationSlider.value = 3;

        float length = lengthSlider.value;
        float width = widthSlider.value;
        float height = heightSlider.value;

        // **确保正方体不会超出长方体**
        float maxCubeSize = Mathf.Min(length, width, height);


        float cubeSize = Mathf.Min(cubeSizeSlider.value, maxCubeSize);
        cubeSizeSlider.value = cubeSize; // **防止 UI 数值超过限制**

        // 更新 UI 显示
        lengthText.text = "BoxScaleX: " + length.ToString("F2");
        widthText.text = "BoxScaleY: " + width.ToString("F2");
        heightText.text = "BoxScaleZ: " + height.ToString("F2");
        cubeSizeText.text = "CubeSize: " + cubeSize.ToString("F2");
        particleNumsText.text = "ParticleNum: " + calculateParticleNums(cubeSize).ToString();
        IterationText.text = "Iteration: " + ((int)(IterationSlider.value)).ToString();

        // 计算长方体和正方体的顶点
        Vector3[] rectVertices = GetBoxVertices(length, width, height);
        Vector3[] cubeVertices = GetBoxVertices(cubeSize, cubeSize, cubeSize);
        // 设置线框
        rectRenderer.SetPositions(rectVertices);
        cubeRenderer.SetPositions(cubeVertices);

        PlayerPrefs.SetFloat("BoxX", length);
        PlayerPrefs.SetFloat("BoxY", width);
        PlayerPrefs.SetFloat("BoxZ", height);
        PlayerPrefs.SetFloat("cubeSize", cubeSize);
        PlayerPrefs.SetInt("Iteration", (int)IterationSlider.value);
    }



    void SetupLineRenderer(LineRenderer lr, Color color)
    {
        lr.positionCount = 16;
        lr.loop = false;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.useWorldSpace = true; // 让 LineRenderer 使用世界坐标

        // 设置默认材质
        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
    }


    // 更新 LineRenderer 的定点
    void UpdateLineRenderer(LineRenderer renderer, Vector3[] positions)
    {
        if (renderer == null)
            return;

        // 旋转所有定点
        Quaternion rotation = transform.rotation;
        for (int i = 0; i < positions.Length; i++)
        {
            // 将每个定点乘以旋转矩阵（基于父物体的旋转）
            positions[i] = rotation * positions[i];
        }

        // 设置新的定点位置
        renderer.SetPositions(positions);
    }

    private void Update()
    {
        particleNumsText.text = "ParticleNum: " + calculateParticleNums(cubeSizeSlider.value).ToString();
    }

    void UpdateWireframes()
    {
        float length = lengthSlider.value;
        float width = widthSlider.value;
        float height = heightSlider.value;

        // **确保正方体不会超出长方体**
        float maxCubeSize = Mathf.Min(length, width, height);
        float cubeSize = Mathf.Min(cubeSizeSlider.value, maxCubeSize);
        cubeSizeSlider.value = cubeSize; // **防止 UI 数值超过限制**

        // 更新 UI 显示
        lengthText.text = "BoxScaleX: " + length.ToString("F2");
        widthText.text = "BoxScaleY: " + width.ToString("F2");
        heightText.text = "BoxScaleZ: " + height.ToString("F2");
        cubeSizeText.text = "CubeSize: " + cubeSize.ToString("F2");
        particleNumsText.text = "ParticleNum: " + calculateParticleNums(cubeSize).ToString();
        IterationText.text = "Iteration: " + ((int)(IterationSlider.value)).ToString();

        // 计算长方体和正方体的顶点
        Vector3[] rectVertices = GetBoxVertices(length, width, height);
        Vector3[] cubeVertices = GetBoxVertices(cubeSize, cubeSize, cubeSize);

        transform.rotation = Quaternion.Euler(0, rotateSlider.value * rotationSpeed, 0);

        UpdateLineRenderer(rectRenderer, rectVertices);
        UpdateLineRenderer(cubeRenderer, cubeVertices);

        PlayerPrefs.SetFloat("BoxX", length);
        PlayerPrefs.SetFloat("BoxY", width);
        PlayerPrefs.SetFloat("BoxZ", height);
        PlayerPrefs.SetFloat("cubeSize", cubeSize);
        PlayerPrefs.SetInt("Iteration", (int)IterationSlider.value);
    }

    int calculateParticleNums(float cubeSize)
    {
        int particleDensity = PlayerPrefs.GetInt("ParticleDensity");
        int targetParticleCount = (int)(cubeSize * cubeSize * cubeSize * particleDensity);
        int particlesPerAxis = (int)Math.Cbrt(targetParticleCount);
        return particlesPerAxis * particlesPerAxis * particlesPerAxis;
    }

    Vector3[] GetBoxVertices(float l, float w, float h)
    {
        l /= 2; w /= 2; h /= 2;  // 计算半尺寸，中心对齐
        return new Vector3[]
        {
            new Vector3(-l, -w, -h), new Vector3(l, -w, -h),
            new Vector3(l, -w, h), new Vector3(-l, -w, h), new Vector3(-l, -w, -h),

            new Vector3(-l, w, -h), new Vector3(l, w, -h),
            new Vector3(l, -w, -h), new Vector3(l, w, -h), new Vector3(l, w, h),
            new Vector3(l, -w, h), new Vector3(l, w, h),

            new Vector3(-l, w, h), new Vector3(-l, -w, h),
            new Vector3(-l, w, h), new Vector3(-l, w, -h),
        };
    }
}
