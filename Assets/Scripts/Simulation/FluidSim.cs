using System;
using UnityEngine;
using Seb.GPUSorting;
using Unity.Mathematics;
using System.Collections.Generic;
using static Seb.Helpers.ComputeHelper;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using TMPro;

namespace Seb.Fluid.Simulation
{
	public class FluidSim : MonoBehaviour
	{
		public event Action<FluidSim> SimulationInitCompleted;

		[Header("BOX Scale")] public float scaleX = 5;
		public float scaleY = 10;
		public float scaleZ = 8;

		[Header("Time Step")] public float normalTimeScale = 1;
		public float slowTimeScale = 0.1f;
		public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
		public int iterationsPerFrame = 3;

		[Header("Simulation Settings")] public float gravity = -10;
		public float smoothingRadius = 0.2f;
		public float targetDensity = 630;
		public float pressureMultiplier = 288;
		public float nearPressureMultiplier = 2.15f;
		public float viscosityStrength = 0;
		[Range(0, 1)] public float collisionDamping = 0.95f;

		[Header("Foam Settings")] public bool foamActive;
		public int maxFoamParticleCount = 1000;
		public float trappedAirSpawnRate = 70;
		public float spawnRateFadeInTime = 0.5f;
		public float spawnRateFadeStartTime = 0;
		public Vector2 trappedAirVelocityMinMax = new(5, 25);
		public Vector2 foamKineticEnergyMinMax = new(15, 80);
		public float bubbleBuoyancy = 1.5f;
		public int sprayClassifyMaxNeighbours = 5;
		public int bubbleClassifyMinNeighbours = 15;
		public float bubbleScale = 0.5f;
		public float bubbleChangeScaleSpeed = 7;

		[Header("Volumetric Render Settings")] public bool renderToTex3D;
		public int densityTextureRes;

		[Header("References")] public ComputeShader compute;
		public Spawner3D spawner;

		[HideInInspector] public RenderTexture DensityMap;
		public Vector3 Scale => transform.localScale;

		// Buffers
		public ComputeBuffer foamBuffer { get; private set; }
		public ComputeBuffer foamSortTargetBuffer { get; private set; }
		public ComputeBuffer foamCountBuffer { get; private set; }
		public ComputeBuffer positionBuffer { get; private set; }
		public ComputeBuffer velocityBuffer { get; private set; }
		public ComputeBuffer densityBuffer { get; private set; }
		public ComputeBuffer predictedPositionsBuffer;
		public ComputeBuffer spatialKeys { get; private set; }
		public ComputeBuffer spatialOffsets { get; private set; }
		public ComputeBuffer sortedIndices { get; private set; }
		public ComputeBuffer debugBuffer { get; private set; }

		ComputeBuffer sortTarget_positionBuffer;
		ComputeBuffer sortTarget_velocityBuffer;
		ComputeBuffer sortTarget_predictedPositionsBuffer;

		// Kernel IDs
		const int externalForcesKernel = 0;
		const int spatialHashKernel = 1;
		const int reorderKernel = 2;
		const int reorderCopybackKernel = 3;
		const int densityKernel = 4;
		const int pressureKernel = 5;
		const int viscosityKernel = 6;
		const int updatePositionsKernel = 7;
		const int renderKernel = 8;
		const int foamUpdateKernel = 9;
		const int foamReorderCopyBackKernel = 10;
		
		// Sorting
		GPUCountSort gpuSort;
		SpatialOffsetCalculator spatialOffsetsCalc;

		// State
		bool isPaused;
		bool pauseNextFrame;
		float smoothRadiusOld;
		float simTimer;
		bool inSlowMode;
		Spawner3D.SpawnData spawnData;
		Dictionary<ComputeBuffer, string> bufferNameLookup;

		// UI
		public TextMeshProUGUI ParticleNumText;
		public TextMeshProUGUI ParticleDensity;
        public TextMeshProUGUI GravityNumText;
        public Slider GravitySlider;
        public TextMeshProUGUI ViscosityText;
		public Slider ViscositySlider;
        public TextMeshProUGUI BoxScaleXText;
        public Slider BoxScaleXSlider;
        public TextMeshProUGUI BoxScaleYText;
        public Slider BoxScaleYSlider;
        public TextMeshProUGUI BoxScaleZText;
        public Slider BoxScaleZSlider;

        //public TextMeshProUGUI fpsText;
        //private float testdeltaTime = 0.0f;

		public CameraOrbitControl cameraOrbitControl;

        //public int designWidth = 720; 
        //public int designHeight = 1280;

        void Start()
        {
            //Screen.SetResolution(designWidth, designHeight, true);
            Application.targetFrameRate = 60;
            Debug.Log("Controls: Space = Play/Pause, Q = SlowMode, R = Reset");
			Debug.Log($"screen width and height: {Screen.width} {Screen.height}");

			//value init
			if(PlayerPrefs.HasKey("ParticleDensity"))
                spawner.particleSpawnDensity = PlayerPrefs.GetInt("ParticleDensity");
            if (PlayerPrefs.HasKey("BoxX"))
                scaleX = PlayerPrefs.GetFloat("BoxX");
            if (PlayerPrefs.HasKey("BoxY"))
                scaleY = PlayerPrefs.GetFloat("BoxY");
            if (PlayerPrefs.HasKey("BoxZ"))
                scaleZ = PlayerPrefs.GetFloat("BoxZ");
			if (PlayerPrefs.HasKey("Iteration"))
                iterationsPerFrame = PlayerPrefs.GetInt("Iteration");


            isPaused = false;
            Initialize();

			//init UI
			initUI();
        }
		void initUI()
		{

            if (ParticleNumText != null)
            {
                ParticleNumText.text = "ParticleNum: " + spawner.debug_num_particles.ToString();
            }
            if (ParticleDensity != null)
            {
                ParticleDensity.text = "ParticleDensity: " + spawner.particleSpawnDensity.ToString();
            }
            //
            if (GravityNumText != null)
            {
                GravityNumText.text = "Gravity: " + gravity.ToString("F2");
            }
            if (GravitySlider != null)
            {
                GravitySlider.onValueChanged.AddListener(sliderGravitySliderChanged);
            }
            //
            if (ViscosityText != null)
            {
                ViscosityText.text = "Viscosity: " + viscosityStrength.ToString("F2");
            }
            if (ViscositySlider != null)
            {
                ViscositySlider.onValueChanged.AddListener(sliderViscositySliderChanged);
            }
            //
            if (BoxScaleXText != null)
            {
                BoxScaleXText.text = "BoxScaleX: " + (scaleX).ToString("F2");
            }
            if (BoxScaleXSlider != null)
            {
                BoxScaleXSlider.onValueChanged.AddListener(sliderBoxScaleXSliderChanged);
            }
            //
            if (BoxScaleYText != null)
            {
                BoxScaleYText.text = "BoxScaleY: " + (scaleY).ToString("F2");
            }
            if (BoxScaleYSlider != null)
            {
                BoxScaleYSlider.onValueChanged.AddListener(sliderBoxScaleYSliderChanged);
            }
            //
            if (BoxScaleZText != null)
            {
                BoxScaleZText.text = "BoxScaleZ: " + (scaleZ).ToString("F2");
            }
            if (BoxScaleZSlider != null)
            {
                BoxScaleZSlider.onValueChanged.AddListener(sliderBoxScaleZSliderChanged);
            }
        }
		void Initialize()
        {
            GraphicsFormat compatibleFormat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.Render);
            Debug.Log($"[FormatChecker] Compatible RenderTexture Format: {compatibleFormat}");

            spawnData = spawner.GetSpawnData();

			// Create buffers
			int numParticles = spawnData.points.Length;
			positionBuffer = CreateStructuredBuffer<float3>(numParticles);
			predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			velocityBuffer = CreateStructuredBuffer<float3>(numParticles);
			densityBuffer = CreateStructuredBuffer<float2>(numParticles);
			spatialKeys = CreateStructuredBuffer<uint>(numParticles);
			spatialOffsets = CreateStructuredBuffer<uint>(numParticles);
			sortedIndices = CreateStructuredBuffer<uint>(numParticles);
			foamBuffer = CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamSortTargetBuffer = CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamCountBuffer = CreateStructuredBuffer<uint>(4096);
			debugBuffer = CreateStructuredBuffer<float3>(numParticles);

			sortTarget_positionBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_velocityBuffer = CreateStructuredBuffer<float3>(numParticles);

			bufferNameLookup = new Dictionary<ComputeBuffer, string>
			{
				{ positionBuffer, "Positions" },
				{ predictedPositionsBuffer, "PredictedPositions" },
				{ velocityBuffer, "Velocities" },
				{ densityBuffer, "Densities" },
				{ spatialKeys, "SpatialKeys" },
				{ spatialOffsets, "SpatialOffsets" },
				{ sortedIndices, "SortedIndices" },
				{ sortTarget_positionBuffer, "SortTarget_Positions" },
				{ sortTarget_predictedPositionsBuffer, "SortTarget_PredictedPositions" },
				{ sortTarget_velocityBuffer, "SortTarget_Velocities" },
				{ foamCountBuffer, "WhiteParticleCounters" },
				{ foamBuffer, "WhiteParticles" },
				{ foamSortTargetBuffer, "WhiteParticlesCompacted" },
				{ debugBuffer, "Debug" }
			};

			// Set buffer data
			SetInitialBufferData(spawnData);

			// External forces kernel
			SetBuffers(compute, externalForcesKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictedPositionsBuffer,
				velocityBuffer
			});

			// Spatial hash kernel
			SetBuffers(compute, spatialHashKernel, bufferNameLookup, new ComputeBuffer[]
			{
				spatialKeys,
				spatialOffsets,
				predictedPositionsBuffer,
				sortedIndices
			});

			// Reorder kernel
			SetBuffers(compute, reorderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				sortedIndices
			});

			// Reorder copyback kernel
			SetBuffers(compute, reorderCopybackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				sortedIndices
			});

			// Density kernel
			SetBuffers(compute, densityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialKeys,
				spatialOffsets
			});

			// Pressure kernel
			SetBuffers(compute, pressureKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialKeys,
				spatialOffsets,
				foamBuffer,
				foamCountBuffer,
				debugBuffer
			});

			// Viscosity kernel
			SetBuffers(compute, viscosityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialKeys,
				spatialOffsets
			});

			// Update positions kernel
			SetBuffers(compute, updatePositionsKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				velocityBuffer
			});

			// Render to 3d tex kernel
			SetBuffers(compute, renderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialKeys,
				spatialOffsets,
			});

			// Foam update kernel
			SetBuffers(compute, foamUpdateKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamCountBuffer,
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialKeys,
				spatialOffsets,
				foamSortTargetBuffer,
				//debugBuffer
			});


			// Foam reorder copyback kernel
			SetBuffers(compute, foamReorderCopyBackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamSortTargetBuffer,
				foamCountBuffer,
			});

			compute.SetInt("numParticles", positionBuffer.count);
			compute.SetInt("MaxWhiteParticleCount", maxFoamParticleCount);

			gpuSort = new GPUCountSort(spatialKeys, sortedIndices, (uint)(spatialKeys.count - 1));
			spatialOffsetsCalc = new SpatialOffsetCalculator(spatialKeys, spatialOffsets);

			UpdateSmoothingConstants();

			// Run single frame of sim with deltaTime = 0 to initialize density texture
			// (so that display can work even if paused at start)
			if (renderToTex3D)
			{
				RunSimulationFrame(0);
			}

			SimulationInitCompleted?.Invoke(this);
		}

		void Update()
		{
			// value update
			transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

            // Run simulation
            if (!isPaused)
			{
				float maxDeltaTime = maxTimestepFPS > 0 ? 1 / maxTimestepFPS : float.PositiveInfinity; // If framerate dips too low, run the simulation slower than real-time
				float dt = Mathf.Min(Time.deltaTime * ActiveTimeScale, maxDeltaTime);

                //testdeltaTime += (Time.unscaledDeltaTime - testdeltaTime) * 0.1f;
                //fpsText.text = ((1.0f / testdeltaTime)).ToString("F2");

                RunSimulationFrame(dt);
			}

			if (pauseNextFrame)
			{
				isPaused = true;
				pauseNextFrame = false;
			}

			HandleInput();

        }

		void RunSimulationFrame(float frameDeltaTime)
		{
			float subStepDeltaTime = frameDeltaTime / iterationsPerFrame;
			UpdateSettings(subStepDeltaTime, frameDeltaTime);

			// Simulation sub-steps
			for (int i = 0; i < iterationsPerFrame; i++)
			{
				simTimer += subStepDeltaTime;
				RunSimulationStep();
			}

			// Foam and spray particles
			if (foamActive)
			{
				Dispatch(compute, maxFoamParticleCount, kernelIndex: foamUpdateKernel);
				Dispatch(compute, maxFoamParticleCount, kernelIndex: foamReorderCopyBackKernel);
			}

			// 3D density map
			if (renderToTex3D)
			{
				UpdateDensityMap();
			}
		}

		void UpdateDensityMap()
		{
			float maxAxis = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
			int w = Mathf.RoundToInt(transform.localScale.x / maxAxis * densityTextureRes);
			int h = Mathf.RoundToInt(transform.localScale.y / maxAxis * densityTextureRes);
			int d = Mathf.RoundToInt(transform.localScale.z / maxAxis * densityTextureRes);
			CreateRenderTexture3D(ref DensityMap, w, h, d, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat, TextureWrapMode.Clamp);
			//CreateRenderTexture3D(ref DensityMap, w, h, d, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, TextureWrapMode.Clamp);
			//Debug.Log(w + " " + h + "  " + d);
			compute.SetTexture(renderKernel, "DensityMap", DensityMap);
			compute.SetInts("densityMapSize", DensityMap.width, DensityMap.height, DensityMap.volumeDepth);
			Dispatch(compute, DensityMap.width, DensityMap.height, DensityMap.volumeDepth, renderKernel);
		}

		void RunSimulationStep()
		{
			Dispatch(compute, positionBuffer.count, kernelIndex: externalForcesKernel);

			Dispatch(compute, positionBuffer.count, kernelIndex: spatialHashKernel);
			gpuSort.Run();
			spatialOffsetsCalc.Run(false);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderCopybackKernel);

			Dispatch(compute, positionBuffer.count, kernelIndex: densityKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: pressureKernel);
			if (viscosityStrength != 0) Dispatch(compute, positionBuffer.count, kernelIndex: viscosityKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: updatePositionsKernel);
		}

		void UpdateSmoothingConstants()
		{
			float r = smoothingRadius;
			float spikyPow2 = 15 / (2 * Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3 = 15 / (Mathf.PI * Mathf.Pow(r, 6));
			float spikyPow2Grad = 15 / (Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3Grad = 45 / (Mathf.PI * Mathf.Pow(r, 6));

			compute.SetFloat("K_SpikyPow2", spikyPow2);
			compute.SetFloat("K_SpikyPow3", spikyPow3);
			compute.SetFloat("K_SpikyPow2Grad", spikyPow2Grad);
			compute.SetFloat("K_SpikyPow3Grad", spikyPow3Grad);
		}

		void UpdateSettings(float stepDeltaTime, float frameDeltaTime)
		{
			if (smoothingRadius != smoothRadiusOld)
			{
				smoothRadiusOld = smoothingRadius;
				UpdateSmoothingConstants();
			}

			Vector3 simBoundsSize = transform.localScale;
			Vector3 simBoundsCentre = transform.position;

			compute.SetFloat("deltaTime", stepDeltaTime);
			compute.SetFloat("whiteParticleDeltaTime", frameDeltaTime);
			compute.SetFloat("simTime", simTimer);
			compute.SetFloat("gravity", gravity);
			compute.SetFloat("collisionDamping", collisionDamping);
			compute.SetFloat("smoothingRadius", smoothingRadius);
			compute.SetFloat("targetDensity", targetDensity);
			compute.SetFloat("pressureMultiplier", pressureMultiplier);
			compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
			compute.SetFloat("viscosityStrength", viscosityStrength);
			compute.SetVector("boundsSize", simBoundsSize);
			compute.SetVector("centre", simBoundsCentre);

			compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
			compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

			// Foam settings
			float fadeInT = (spawnRateFadeInTime <= 0) ? 1 : Mathf.Clamp01((simTimer - spawnRateFadeStartTime) / spawnRateFadeInTime);
			compute.SetVector("trappedAirParams", new Vector3(trappedAirSpawnRate * fadeInT * fadeInT, trappedAirVelocityMinMax.x, trappedAirVelocityMinMax.y));
			compute.SetVector("kineticEnergyParams", foamKineticEnergyMinMax);
			compute.SetFloat("bubbleBuoyancy", bubbleBuoyancy);
			compute.SetInt("sprayClassifyMaxNeighbours", sprayClassifyMaxNeighbours);
			compute.SetInt("bubbleClassifyMinNeighbours", bubbleClassifyMinNeighbours);
			compute.SetFloat("bubbleScaleChangeSpeed", bubbleChangeScaleSpeed);
			compute.SetFloat("bubbleScale", bubbleScale);
		}

		void SetInitialBufferData(Spawner3D.SpawnData spawnData)
		{
			positionBuffer.SetData(spawnData.points);
			predictedPositionsBuffer.SetData(spawnData.points);
			velocityBuffer.SetData(spawnData.velocities);

			foamBuffer.SetData(new FoamParticle[foamBuffer.count]);

			debugBuffer.SetData(new float3[debugBuffer.count]);
			foamCountBuffer.SetData(new uint[foamCountBuffer.count]);
			simTimer = 0;
		}

		void HandleInput()
		{
			if (Input.GetKeyDown(KeyCode.Space))
			{
				isPaused = !isPaused;
			}

			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				isPaused = false;
				pauseNextFrame = true;
			}

			if (Input.GetKeyDown(KeyCode.R))
			{
				pauseNextFrame = true;
				SetInitialBufferData(spawnData);
				// Run single frame of sim with deltaTime = 0 to initialize density texture
				// (so that display can work even if paused at start)
				if (renderToTex3D)
				{
					RunSimulationFrame(0);
				}
			}

			if (Input.GetKeyDown(KeyCode.Q))
			{
				inSlowMode = !inSlowMode;
			}
		}




		//UI Function

		public void buttonPause()
		{
            isPaused = true;
        }
        public void buttonPlay()
        {
            isPaused = false;
        }
        public void buttonReset()
        {
            pauseNextFrame = true;
            SetInitialBufferData(spawnData);
            if (renderToTex3D)
            {
                RunSimulationFrame(0);
            }
        }
        public void buttonSlowMode()
        {
            inSlowMode = !inSlowMode;
        }
        public void buttonNextFrame()
		{
			isPaused = false;
            pauseNextFrame = true;
        }
        void sliderGravitySliderChanged(float value)
        {
			cameraOrbitControl.enabled = false;
            gravity = value;
            GravityNumText.text = "Gravity: " + gravity.ToString("F2");
			cameraOrbitControl.enabled = true;
        }
		void sliderViscositySliderChanged(float value)
		{
			cameraOrbitControl.enabled = false;
            viscosityStrength = value;
            ViscosityText.text = "Viscosity: " + value.ToString("F2");
            cameraOrbitControl.enabled = true;
        }
		void sliderBoxScaleXSliderChanged(float value)
		{
			cameraOrbitControl.enabled = false;
            scaleX = value;
            BoxScaleXText.text = "BoxScaleX: " + value.ToString("F2");
            cameraOrbitControl.enabled = true;
        }
        void sliderBoxScaleYSliderChanged(float value)
        {
            cameraOrbitControl.enabled = false;
            scaleY = value;
            BoxScaleYText.text = "BoxScaleY: " + value.ToString("F2");
            cameraOrbitControl.enabled = true;
        }
        void sliderBoxScaleZSliderChanged(float value)
        {
            cameraOrbitControl.enabled = false;
            scaleZ = value;
            BoxScaleZText.text = "BoxScaleZ: " + value.ToString("F2");
            cameraOrbitControl.enabled = true;
        }




        private float ActiveTimeScale => inSlowMode ? slowTimeScale : normalTimeScale;

		void OnDestroy()
		{
			foreach (var kvp in bufferNameLookup)
			{
				Release(kvp.Key);
			}

			gpuSort.Release();
		}


		public struct FoamParticle
		{
			public float3 position;
			public float3 velocity;
			public float lifetime;
			public float scale;
		}

		void OnDrawGizmos()
		{
			// Draw Bounds
			var m = Gizmos.matrix;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = new Color(0, 1, 0, 0.5f);
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.matrix = m;
		}
	}
}