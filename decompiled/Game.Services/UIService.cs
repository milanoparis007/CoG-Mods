using System.Collections.Generic;
using Game.Session;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Services;

public class UIService : AbstractService, IUpdateService, IService
{
	private sealed class CanvasInfo
	{
		public Scene scene;

		public string name;

		public Canvas canvas;
	}

	private List<IUIDialog> _dialogs;

	private List<IUIDialogUpdated> _dialogsUpdated;

	private SmartStack<IUIPopup> _popups;

	private float _uiScale;

	private Dictionary<string, Dictionary<string, GameObject>> _uireferences;

	private List<CanvasInfo> _canvasInfos;

	private List<object> _raycastSuppressRequests;

	private bool _isMouseIgnored;

	public PlayerStartupDetails storedStartupDetails;

	public bool ShowingCanvases { get; private set; }

	public override bool IsLoadingDone => _uireferences.ContainsKey("GameUI");

	public float UIScaleFactor => _uiScale;

	public IUIPopup TopPopupUnsafe
	{
		get
		{
			if (_popups.Count <= 0)
			{
				return null;
			}
			return _popups.Peek();
		}
	}

	public override void OnStartLoading()
	{
		_raycastSuppressRequests = new List<object>();
		_uireferences = new Dictionary<string, Dictionary<string, GameObject>>();
		_canvasInfos = new List<CanvasInfo>();
		_dialogs = new List<IUIDialog>();
		_dialogsUpdated = new List<IUIDialogUpdated>();
		_popups = new SmartStack<IUIPopup>();
		SceneManager.sceneLoaded += delegate(Scene uiscene, LoadSceneMode mode)
		{
			InitializeUIScene(uiscene);
		};
		StartLoadingUIScene("GameUI");
	}

	public override void OnInitialized()
	{
		SRDebug.Init();
		Logger.LogAlways(Game.instance.GetPlatformStats());
		Game.serv.loc.OnLanguageChanged.Add(OnLanguageChanged);
		RefreshAllCanvasScale();
		StartLoadingUIScene("MovingUI");
		StartLoadingUIScene("SessionUI");
	}

	public void OnUpdate()
	{
		UpdateIgnoreMouseRequests();
		foreach (IUIDialogUpdated item in _dialogsUpdated)
		{
			item.OnFrameUpdate();
		}
	}

	public override void OnReleased()
	{
		Game.serv.loc.OnLanguageChanged.Remove(OnLanguageChanged);
		HideAllDialogsAndPopups();
		_popups = null;
		_dialogs = null;
		_dialogsUpdated = null;
		_uireferences.ClearDeep();
		_uireferences = null;
		_canvasInfos = null;
		_raycastSuppressRequests = null;
	}

	public void HideAllDialogsAndPopups()
	{
		while (_popups.Count > 0)
		{
			RemoveTopPopup();
		}
		while (_dialogs.Count > 0)
		{
			HideUIDialog(_dialogs.LastOrDefaultFast());
		}
	}

	private void StartLoadingUIScene(string name)
	{
		Scene sceneByName = SceneManager.GetSceneByName(name);
		if (sceneByName.isLoaded)
		{
			InitializeUIScene(sceneByName);
		}
		else
		{
			SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
		}
	}

	private void InitializeUIScene(Scene scene)
	{
		string name = scene.name;
		if (!UIReference.IsUISceneName(name))
		{
			return;
		}
		GameObject[] rootGameObjects = scene.GetRootGameObjects();
		if (rootGameObjects.Length < 1)
		{
			return;
		}
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			Canvas component = rootGameObjects[i].GetComponent<Canvas>();
			if (component == null)
			{
				continue;
			}
			_canvasInfos.Add(new CanvasInfo
			{
				scene = scene,
				name = scene.name,
				canvas = component
			});
			RefreshCanvasScale(component);
			foreach (Transform item in component.transform)
			{
				GameObject gameObject = item.gameObject;
				_uireferences.Add(name, gameObject.name, gameObject.gameObject);
				gameObject.SetActive(value: false);
			}
		}
	}

	public bool IsMouseIgnored()
	{
		return _raycastSuppressRequests.Count > 0;
	}

	public void ToggleIgnoreMouseRequest(bool add, object requester)
	{
		_raycastSuppressRequests.Remove(requester);
		if (add)
		{
			_raycastSuppressRequests.Add(requester);
		}
	}

	private void UpdateIgnoreMouseRequests()
	{
		bool flag = IsMouseIgnored();
		if (_isMouseIgnored != flag)
		{
			RefreshRaycasts();
			_isMouseIgnored = flag;
		}
	}

	private void RefreshRaycasts()
	{
		bool enabled = !IsMouseIgnored();
		foreach (CanvasInfo canvasInfo in _canvasInfos)
		{
			if (IsMovingUICanvas(canvasInfo))
			{
				GraphicRaycaster component = canvasInfo.canvas.gameObject.GetComponent<GraphicRaycaster>();
				if (component != null)
				{
					component.enabled = enabled;
				}
			}
		}
	}

	private bool IsMovingUICanvas(CanvasInfo info)
	{
		return info.name == "MovingUI";
	}

	public void RefreshAllCanvasScale()
	{
		_uiScale = (float)Game.serv.saveload.prefs.game.uiscale / 100f;
		if (_canvasInfos == null)
		{
			return;
		}
		foreach (CanvasInfo canvasInfo in _canvasInfos)
		{
			RefreshCanvasScale(canvasInfo.canvas);
		}
	}

	private void RefreshCanvasScale(Canvas canvas)
	{
		canvas.GetComponent<CanvasScaler>().scaleFactor = _uiScale;
	}

	public GameObject GetUI(UIReference reference)
	{
		GameObject gameObject = _uireferences.FindOrNull(reference.GetSceneName(), reference.Path);
		_ = gameObject == null;
		return gameObject;
	}

	public Canvas GetMovingUICanvas()
	{
		return GetCanvas(UIScene.MovingUI);
	}

	public Canvas GetSessionUICanvas()
	{
		return GetCanvas(UIScene.SessionUI);
	}

	public Canvas GetCanvas(UIScene scene)
	{
		string sceneName = UIReference.GetSceneName(scene);
		foreach (CanvasInfo canvasInfo in _canvasInfos)
		{
			if (canvasInfo.name == sceneName)
			{
				return canvasInfo.canvas;
			}
		}
		return null;
	}

	private void OnLanguageChanged()
	{
		foreach (CanvasInfo canvasInfo in _canvasInfos)
		{
			GameObject gameObject = canvasInfo.canvas.gameObject;
			if (!gameObject.activeSelf)
			{
				continue;
			}
			gameObject.WalkChildren(delegate(Transform tr)
			{
				GameObject gameObject2 = tr.gameObject;
				if (gameObject2.activeSelf)
				{
					LocalizeText component = gameObject2.GetComponent<LocalizeText>();
					if (component != null)
					{
						component.Refresh();
					}
				}
			});
		}
	}

	public void ShowUIDialog(IUIDialog dialog)
	{
		if (_dialogs == null || dialog == null)
		{
			Logger.Warning("Uninitialized, cannot show dialog " + dialog);
		}
		else if (!dialog.IsShowing)
		{
			_dialogs.Add(dialog);
			if (dialog is IUIDialogUpdated item)
			{
				_dialogsUpdated.Add(item);
			}
			dialog.RequestShow(this);
		}
		else
		{
			Logger.Warning("Double showing dialog " + dialog);
		}
	}

	public void HideUIDialog(IUIDialog dialog)
	{
		if (_dialogs == null || dialog == null)
		{
			Logger.Warning("Uninitialized, cannot hide dialog " + dialog);
			return;
		}
		if (dialog.IsShowing)
		{
			dialog.RequestsHide(this);
		}
		else
		{
			Logger.Warning("Double hiding dialog " + dialog);
		}
		if (dialog is IUIDialogUpdated item)
		{
			_dialogsUpdated.Remove(item);
		}
		_dialogs.Remove(dialog);
	}

	public bool ContainsPopup<T>() where T : class, IUIPopup
	{
		return GetActivePopup<T>() != null;
	}

	public T AddPopup<T>() where T : class, IUIPopup, new()
	{
		return AddPopup(new T());
	}

	public T AddPopup<T>(T popup) where T : class, IUIPopup
	{
		if (_popups == null)
		{
			Logger.Warning("Uninitialized, cannot show popup " + popup);
			return null;
		}
		_popups.Push(popup);
		return popup;
	}

	public T GetActivePopup<T>() where T : class, IUIPopup
	{
		foreach (IUIPopup popup in _popups)
		{
			if (popup is T result)
			{
				return result;
			}
		}
		return null;
	}

	public bool RemovePopup<T>() where T : class, IUIPopup
	{
		T activePopup = GetActivePopup<T>();
		if (activePopup == null)
		{
			return false;
		}
		return RemovePopup(activePopup);
	}

	public bool RemovePopup(IUIPopup popup)
	{
		if (_popups == null)
		{
			Logger.Warning("Uninitialized, cannot remove popup " + popup);
			return false;
		}
		return _popups.Remove(popup);
	}

	public IUIPopup RemoveTopPopup()
	{
		if (_popups == null)
		{
			Logger.Warning("Uninitialized, cannot remove top popup");
			return null;
		}
		return _popups.Pop();
	}

	internal void ToggleCanvases(bool all)
	{
		ToggleCanvases(all, !ShowingCanvases);
	}

	internal void ToggleCanvases(bool all, bool show)
	{
		ShowingCanvases = show;
		foreach (CanvasInfo canvasInfo in _canvasInfos)
		{
			if (all || !IsMovingUICanvas(canvasInfo))
			{
				canvasInfo.canvas.gameObject.SetActive(show);
			}
		}
	}

	public void ShowOrHideSessionCanvas(bool show)
	{
		ShowOrHideCanvas("SessionUI", show);
	}

	public void ShowOrHideCanvas(string scene, bool show)
	{
		foreach (CanvasInfo canvasInfo in _canvasInfos)
		{
			if (canvasInfo.name == scene)
			{
				canvasInfo.canvas.gameObject.SetActive(show);
			}
		}
	}
}
