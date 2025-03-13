using Seb.Fluid.Simulation;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Seb.Fluid.Simulation.Spawner3D;

public class SceneLoader : MonoBehaviour
{
    //UI
    public TextMeshProUGUI DensityNumText;
    public Slider DensitySlider;

    public void LoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(0);  // 0 �ų�����Ϊ MainMenu
    }
    public void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // �ڱ༭��ģʽ��ֹͣ����
#else
            Application.Quit(); // ���ƶ��˹ر�Ӧ��
#endif
    }
    // Start is called before the first frame update
    void Start()
    {
        if (DensitySlider != null)
        {
            DensitySlider.onValueChanged.AddListener(sliderParticleDensityChanged);
        }
        if (DensityNumText != null)
        {
            DensityNumText.text = "Particle Density: " + DensitySlider.value.ToString();
            PlayerPrefs.SetInt("ParticleDensity", (int)DensitySlider.value);
            PlayerPrefs.Save();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void sliderParticleDensityChanged(float value)
    {
        value = Mathf.Round(value / 10f) * 10f;
        DensitySlider.value = value;
        int densityValue = (int)value;
        DensityNumText.text = "Particle Density: " + densityValue.ToString();
        PlayerPrefs.SetInt("ParticleDensity", densityValue);
        PlayerPrefs.Save();
    }
}
