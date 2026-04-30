using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Filesystem;
using IndirectRendering;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace Game.Services;

public class CameraService : AbstractService, IUpdateService, IService, ILateUpdateService
{
	private struct DirtyPos
	{
		public Vector3 oldPos;

		public Vector3 newPos;

		public Quaternion oldAngle;

		public Quaternion newAngle;

		public float oldZoom;

		public float newZoom;

		public bool isDirty;
	}

	public class ComputeShaderVerifier
	{
		public enum State
		{
			NotStarted,
			WaitingForScene,
			ResultPass,
			ResultFail,
			ResultSkipped,
			Done
		}

		private const string TEST_SCENE_NAME = "TestComputeValid";

		private State _state;

		private Scene _scene;

		private TestCompute _testcomp;

		public bool IsNotStarted()
		{
			return _state == State.NotStarted;
		}

		public bool IsWaitingForScene()
		{
			return _state == State.WaitingForScene;
		}

		public bool HasResult()
		{
			if (_state != State.ResultPass && _state != State.ResultFail)
			{
				return _state == State.ResultSkipped;
			}
			return true;
		}

		public void OnFrameUpdate()
		{
			if (_state != State.Done)
			{
				if (IsNotStarted())
				{
					StartVerifyComputeShaders();
				}
				if (IsWaitingForScene())
				{
					TryQueryScene();
				}
				if (HasResult())
				{
					ProcessResult();
				}
			}
		}

		private void StartVerifyComputeShaders()
		{
			if (IsNotStarted())
			{
				_state = State.WaitingForScene;
				if (!Game.serv.saveload.progress.stats.IsFirstSession)
				{
					_state = State.ResultSkipped;
					return;
				}
				if (!SystemInfo.supportsComputeShaders)
				{
					_state = State.ResultFail;
					return;
				}
				SceneManager.sceneLoaded += OnSceneLoaded;
				SceneManager.LoadSceneAsync("TestComputeValid", LoadSceneMode.Additive);
			}
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name != "TestComputeValid")
			{
				return;
			}
			SceneManager.sceneLoaded -= OnSceneLoaded;
			_scene = scene;
			GameObject[] rootGameObjects = _scene.GetRootGameObjects();
			for (int i = 0; i < rootGameObjects.Length; i++)
			{
				TestCompute componentInChildren = rootGameObjects[i].GetComponentInChildren<TestCompute>();
				if (componentInChildren != null)
				{
					_testcomp = componentInChildren;
					break;
				}
			}
			if (_testcomp == null)
			{
				_state = State.ResultSkipped;
			}
		}

		private void TryQueryScene()
		{
			if (IsWaitingForScene() && !(_testcomp == null) && _testcomp.State == TestCompute.TestState.Done)
			{
				_state = (_testcomp.PassedCSTests ? State.ResultPass : State.ResultFail);
			}
		}

		private void ProcessResult()
		{
			if (HasResult())
			{
				Logger.LogAlways("Identified compute shader state: " + _state);
				if (_state == State.ResultFail)
				{
					Game.serv.camera.OnComputeShaderFailureDetected();
					Logger.LogAlways("Compute shaders: disabled due to video card performance");
				}
				else
				{
					Logger.LogAlways("Compute shaders: not affected");
				}
				if (_scene.isLoaded)
				{
					SceneManager.UnloadSceneAsync(_scene);
				}
				_scene = default(Scene);
				_testcomp = null;
				_state = State.Done;
			}
		}
	}

	public static List<int> UI_SCALES = new List<int> { 75, 80, 90, 100, 125, 150, 175, 200, 250, 300 };

	public Listeners OnCameraMoved;

	public Listeners<float> OnCameraZoomPercentage;

	public Listeners OnScreenChanged;

	private GameObject _dolly;

	private GameObject _boom;

	private Camera _camera;

	private Light _mainLight;

	private List<object> _blurrers;

	private ComputeShaderVerifier _shadertest;

	private DirtyPos _move;

	private IntSize _screenSize;

	private float _lastZoom = float.NaN;

	private RenderTexture _scrTx;

	private Texture2D _scr;

	public CameraSettings Settings { get; private set; }

	public GameObject DollyGO => _dolly;

	public GameObject BoomGO => _boom;

	public bool IsIndirectInstancingAvailable
	{
		get
		{
			if (IndirectRenderer.IsAvailable == true)
			{
				return !Game.serv.globals.settings.general.debug.disableIndirectInstancing;
			}
			return false;
		}
	}

	public static int FindMaxUIScale(int width, int height)
	{
		int num = Math.Min(width, height);
		if (num >= 1080)
		{
			if (num >= 2160)
			{
				return 300;
			}
			return 200;
		}
		return 150;
	}

	public static int FindStartingUIScale(int width, int height)
	{
		int num = Math.Min(width, height);
		int num2 = ((num < 1080) ? 100 : ((num < 1440) ? 125 : ((num < 2160) ? 150 : 200)));
		if (num2 > 150)
		{
			num2 = 150;
		}
		return num2;
	}

	public override void OnCreated()
	{
		_dolly = GameObject.Find("Camera Dolly");
		_boom = GameObject.Find("Camera Boom");
		_camera = Camera.main;
		_mainLight = GameObject.Find("Directional Light").GetComponent<Light>();
		_screenSize = new IntSize(0, 0);
		Settings = _camera.gameObject.GetComponent<CameraSettings>();
		_blurrers = new List<object>();
		_shadertest = new ComputeShaderVerifier();
		OnCameraMoved = new Listeners();
		OnCameraZoomPercentage = new Listeners<float>();
		OnScreenChanged = new Listeners();
	}

	public override void OnLoaded()
	{
		base.OnLoaded();
		ProcessVisualOverrides();
	}

	public override void OnDestroyed()
	{
		OnCameraMoved.Clear();
		OnCameraZoomPercentage.Clear();
		OnScreenChanged.Clear();
		_shadertest = null;
		_blurrers = null;
		_mainLight = null;
		_camera = null;
		_boom = null;
		_dolly = null;
	}

	public void ResetCamera(WorldSize boardSize, CameraTween? tween = null, bool pitch = true, bool rotation = true)
	{
		SetPosition(new WorldPos(boardSize.width * 0.5f, boardSize.height * 0.4f), tween);
		if (rotation)
		{
			SetRotation(Settings.startYRotation, tween);
		}
		if (pitch)
		{
			SetPitch(Settings.startXPitch, tween);
		}
		SetZoom(Settings.startZZoom, tween);
	}

	public void OnUpdate()
	{
		IntSize intSize = new IntSize(Screen.width, Screen.height);
		if (!object.Equals(_screenSize, intSize))
		{
			_screenSize = intSize;
			OnScreenChanged.Invoke();
		}
		_shadertest.OnFrameUpdate();
	}

	public void OnLateUpdate()
	{
		if (_move.isDirty)
		{
			bool num = _move.newPos != _move.oldPos;
			bool flag = _move.newAngle != _move.oldAngle;
			bool flag2 = _move.newZoom != _move.oldZoom;
			if (num || flag || flag2)
			{
				_move.oldZoom = _move.newZoom;
				_move.oldPos = _move.newPos;
				_move.oldAngle = _move.newAngle;
				UpdateFOV();
				OnCameraMoved.Invoke();
			}
			_move.isDirty = false;
		}
	}

	private void UpdateFOV()
	{
		float z = _boom.transform.localScale.z;
		DepthOfField setting = Settings.postFX.profile.GetSetting<DepthOfField>();
		if (z != (float)setting.focusDistance)
		{
			setting.focusDistance.Override(z);
		}
	}

	public void SetPostFX(bool isEnabled)
	{
		Settings.postFX.profile.GetSetting<AmbientOcclusion>().active = isEnabled;
	}

	private void ProcessVisualOverrides()
	{
		if (!Game.settings.IsDesktop)
		{
			Logger.Error("FRAMERATE CAPPING needs to be implemented on non-desktop, if desired");
			return;
		}
		VisualSettings visuals = Game.serv.globals.settings.general.visuals;
		if (visuals.forceVsync > 0)
		{
			QualitySettings.vSyncCount = MathUtil.Clamp(visuals.forceVsync, 0, 4);
		}
		if (visuals.forceFPSLimit > 0)
		{
			int num = MathUtil.ClampMin(visuals.forceFPSLimit, 0);
			if (num == 0)
			{
				num = -1;
			}
			Application.targetFrameRate = num;
		}
	}

	public void VerifyPrefsFile()
	{
		int num;
		int num2;
		bool fullscreen;
		bool postEnabled;
		int vsync;
		int num3;
		if (Game.serv.saveload.progress.stats.IsFirstSession)
		{
			Resolution resolution = Screen.resolutions[Screen.resolutions.Length - 1];
			num = resolution.width;
			num2 = resolution.height;
			fullscreen = true;
			postEnabled = true;
			vsync = 1;
			num3 = FindStartingUIScale(num, num2);
		}
		else
		{
			GamePreferences game = Game.serv.saveload.prefs.game;
			num = game.width;
			num2 = game.height;
			fullscreen = game.fullscreen;
			postEnabled = game.postEnabled;
			vsync = game.vsync;
			num3 = game.uiscale;
		}
		if (num == 0 || num2 == 0 || !UI_SCALES.Contains(num3))
		{
			num = 1280;
			num2 = 720;
			fullscreen = false;
			num3 = 100;
		}
		ApplySettings(num, num2, num3, vsync, fullscreen, postEnabled);
	}

	private void ApplySettings(int width, int height, int uiscale, int vsync, bool fullscreen, bool postEnabled)
	{
		GamePreferences game = Game.serv.saveload.prefs.game;
		game.SetResolution(width, height, fullscreen);
		game.SetPostFX(postEnabled);
		game.SetVSync(vsync);
		game.SetUIScale(uiscale);
	}

	private void OnComputeShaderFailureDetected()
	{
		Game.serv.saveload.prefs.game.computeShaders = false;
		Game.serv.saveload.SavePrefs();
	}

	public void AddBlurRequest(object sentinel)
	{
		if (!_blurrers.Contains(sentinel))
		{
			_blurrers.Add(sentinel);
		}
	}

	public void RemoveBlurRequest(object sentinel)
	{
		if (_blurrers.Contains(sentinel))
		{
			_blurrers.Remove(sentinel);
		}
	}

	public void ToggleBlur(object sentinel, bool show)
	{
		if (show)
		{
			AddBlurRequest(sentinel);
		}
		else
		{
			RemoveBlurRequest(sentinel);
		}
	}

	public Ray ScreenToRay(Vector3 pos)
	{
		return _camera.ScreenPointToRay(pos);
	}

	public Vector2 WorldToScreenPos(WorldPos pos, float height = 0f)
	{
		return _camera.WorldToScreenPoint(WorldToSceneVector(pos, height));
	}

	public Vector3 ScreenToSceneVector(Vector2 pos, float height = 0f)
	{
		Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
		Ray ray = _camera.ScreenPointToRay(pos);
		Vector3 result = default(Vector3);
		if (plane.Raycast(ray, out var enter))
		{
			return ray.GetPoint(enter);
		}
		return result;
	}

	public WorldPos ScreenToWorldPos(Vector2 screenpos, float height = 0f)
	{
		Vector3 vector = ScreenToSceneVector(screenpos, height);
		return new WorldPos(vector.x, vector.z);
	}

	public WorldPos ScreenCenterToWorldPos(float height = 0f)
	{
		Vector2 screenpos = new Vector2((float)Screen.width / 2f, (float)Screen.height / 2f);
		return ScreenToWorldPos(screenpos, height);
	}

	public Vector3 WorldToSceneVector(WorldPos pos, float height = 0f)
	{
		return new Vector3(pos.x, height, pos.y);
	}

	public CameraPos WorldToCameraPos(WorldPos pos)
	{
		return new CameraPos(pos.x, pos.y);
	}

	public WorldPos SceneToWorldPos(Vector3 pos)
	{
		return new WorldPos(pos.x, pos.z);
	}

	public Vector2 SceneToScreenPos(Vector3 scene)
	{
		return _camera.WorldToScreenPoint(scene);
	}

	public Vector3 GetCameraForward()
	{
		return _camera.transform.forward;
	}

	public Vector3 GetCameraUp()
	{
		return _camera.transform.up;
	}

	public CameraPos GetPosition()
	{
		return CameraPos.FromVectorXZ(_dolly.transform.position);
	}

	public void SetPosition(WorldPos wpos, CameraTween? tween = null)
	{
		SetPosition(WorldToCameraPos(wpos), tween);
	}

	public void SetPosition(CameraPos pos, CameraTween? tween = null)
	{
		CameraTween valueOrDefault = tween.GetValueOrDefault();
		if (!tween.HasValue || valueOrDefault.interruptAllTweens)
		{
			LeanTween.cancel(_dolly);
		}
		if (!valueOrDefault.IsSet)
		{
			SetPositionHelper(pos.AsVector3XZ);
			return;
		}
		Vector3 asVector3XZ = GetPosition().AsVector3XZ;
		Vector3 asVector3XZ2 = pos.AsVector3XZ;
		LeanTween.value(_dolly, SetPositionHelper, asVector3XZ, asVector3XZ2, valueOrDefault.timeSeconds).setEase(LeanTweenType.easeOutQuad);
	}

	private void SetPositionHelper(Vector3 posxz)
	{
		_dolly.transform.position = posxz;
		_move.newPos = posxz;
		_move.isDirty = true;
	}

	public float GetRotation()
	{
		return _boom.transform.rotation.eulerAngles.y;
	}

	public void SetRotation(float angle, CameraTween? tween = null)
	{
		CameraTween valueOrDefault = tween.GetValueOrDefault();
		if (!tween.HasValue || valueOrDefault.interruptAllTweens)
		{
			LeanTween.cancel(_boom);
		}
		if (!valueOrDefault.IsSet)
		{
			SetRotationHelper(angle);
			return;
		}
		float rotation = GetRotation();
		LeanTween.value(_boom, SetRotationHelper, rotation, angle, valueOrDefault.timeSeconds).setEase(valueOrDefault.easing);
	}

	public void IncrementRotation(float angleDelta, CameraTween? tween = null)
	{
		SetRotation(GetRotation() + angleDelta, tween);
	}

	private void SetRotationHelper(float angle)
	{
		Quaternion rotation = _boom.transform.rotation;
		rotation.eulerAngles = rotation.eulerAngles.SetY(angle);
		_boom.transform.rotation = rotation;
		_move.newAngle = rotation;
		_move.isDirty = true;
	}

	public float GetPitch()
	{
		return _boom.transform.eulerAngles.x;
	}

	public void SetPitch(float pitch, CameraTween? tween = null)
	{
		CameraTween valueOrDefault = tween.GetValueOrDefault();
		if (!tween.HasValue || valueOrDefault.interruptAllTweens)
		{
			LeanTween.cancel(_boom);
		}
		if (!valueOrDefault.IsSet)
		{
			SetPitchHelper(pitch);
			return;
		}
		float pitch2 = GetPitch();
		LeanTween.value(_boom, SetPitchHelper, pitch2, pitch, valueOrDefault.timeSeconds).setEase(valueOrDefault.easing);
	}

	public void IncrementPitch(float pitchDelta, CameraTween? tween = null)
	{
		SetPitch(GetPitch() + pitchDelta, tween);
	}

	private void SetPitchHelper(float angle)
	{
		float x = Settings.ClampPitch(angle);
		Quaternion rotation = _boom.transform.rotation;
		rotation.eulerAngles = rotation.eulerAngles.SetX(x);
		_boom.transform.rotation = rotation;
		_move.newAngle = rotation;
		_move.isDirty = true;
	}

	public float GetZoom()
	{
		return _boom.transform.localScale.z;
	}

	public void SetZoom(float zoom, CameraTween? tween = null)
	{
		CameraTween valueOrDefault = tween.GetValueOrDefault();
		if (!tween.HasValue || valueOrDefault.interruptAllTweens)
		{
			LeanTween.cancel(_boom);
		}
		if (!valueOrDefault.IsSet)
		{
			SetZoomHelper(zoom);
			return;
		}
		float zoom2 = GetZoom();
		LeanTween.value(_boom, SetZoomHelper, zoom2, zoom, valueOrDefault.timeSeconds).setEase(valueOrDefault.easing);
	}

	public void IncrementZoom(float zoomDelta, CameraTween? tween = null)
	{
		SetZoom(GetZoom() + zoomDelta, tween);
	}

	private void SetZoomHelper(float zoom)
	{
		if (_lastZoom != zoom)
		{
			_lastZoom = zoom;
			float num = Settings.ClampZoom(zoom);
			Vector3 localScale = _boom.transform.localScale.SetZ(num);
			_boom.transform.localScale = localScale;
			float zoomPercentage = Settings.GetZoomPercentage(num);
			_camera.farClipPlane = Settings.GetFarClipPlane(zoomPercentage);
			_camera.fieldOfView = Settings.GetFOV(zoomPercentage);
			OnCameraZoomPercentage.Invoke(zoomPercentage);
			float shadowBias = Settings.GetShadowBias(zoomPercentage);
			_mainLight.shadowBias = shadowBias;
			_move.newZoom = zoom;
			_move.isDirty = true;
		}
	}

	public byte[] TakeScreenshotForSavefile(int width = 400, int height = 400)
	{
		return TakeScreenshot(width, height, asJpg: true);
	}

	public void TakeScreenshotAndFillTexture(Texture2D tx)
	{
		byte[] data = TakeScreenshot(tx.width, tx.height, asJpg: false);
		tx.LoadImage(data);
	}

	public byte[] TakeScreenshot(int width, int height, bool asJpg)
	{
		if (_scrTx != null && (_scrTx.width != width || _scrTx.height != height))
		{
			UnityEngine.Object.Destroy(_scrTx);
			UnityEngine.Object.Destroy(_scr);
			_scrTx = null;
			_scr = null;
		}
		if (_scrTx == null)
		{
			_scrTx = new RenderTexture(width, height, 24);
			_scr = new Texture2D(width, height);
		}
		Camera camera = _camera;
		camera.targetTexture = _scrTx;
		camera.Render();
		camera.targetTexture = null;
		RenderTexture.active = _scrTx;
		_scr.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
		RenderTexture.active = null;
		if (!asJpg)
		{
			return _scr.EncodeToPNG();
		}
		return _scr.EncodeToJPG(90);
	}
}
