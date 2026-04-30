using System.Collections.Generic;
using Game.Core;
using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Mouseovers;

public class MouseoverService : AbstractService
{
	private MouseoverType _current;

	private Dictionary<MouseoverType, BaseMouseover> _mouseovers;

	private bool _enabled;

	private GameObject _go;

	private GameObject _container;

	private GameObject _templates;

	public bool IsShowing => _current != MouseoverType.None;

	public MouseoverType ShowingType => _current;

	public override void OnInitialized()
	{
		_enabled = true;
		_mouseovers = new Dictionary<MouseoverType, BaseMouseover>(new MouseoverTypeEqualityComparer());
		_go = Game.serv.ui.GetUI(UIElements.Mouseovers);
		_container = _go.GetChild("Container");
		_templates = _go.GetChild("Templates");
		_go.SetActive(value: true);
		_container.SetActive(value: true);
		_templates.SetActive(value: false);
		Register(MouseoverType.TextMouseover, new TextMouseover(rightAligned: false));
		Register(MouseoverType.TextMouseoverTR, new TextMouseover(rightAligned: true));
	}

	public override void OnReleased()
	{
		Unregister(MouseoverType.TextMouseover);
		_templates = null;
		_container = null;
		_go = null;
	}

	internal GameObject GetContainer()
	{
		return _container;
	}

	private void PushMouseoversToFront()
	{
		_go.transform.SetAsLastSibling();
	}

	private Transform GetContainerTransform()
	{
		if (_container != null)
		{
			PushMouseoversToFront();
			return _container.transform;
		}
		Logger.Warning("Mouse over creation before init?");
		return null;
	}

	internal BaseMouseover GetMouseoverUnsafe(MouseoverType type)
	{
		return _mouseovers.FindOrNull(type);
	}

	public void Register(MouseoverType type, BaseMouseover mouseover)
	{
		if (_mouseovers.ContainsKey(type))
		{
			Unregister(type);
		}
		GameObject go = Object.Instantiate(_templates.GetChild(mouseover.MouseoverAssetName.path), _container.transform);
		_mouseovers.Add(type, mouseover);
		mouseover.OnAfterCreate(go);
		mouseover.go.SetActive(value: false);
	}

	public void Unregister(MouseoverType type)
	{
		if (IsShowing && _current == type)
		{
			Hide();
		}
		BaseMouseover baseMouseover = _mouseovers.FindOrNull(type);
		if (baseMouseover == null)
		{
			Logger.Error("Missing mouseover during unregister: " + type);
			return;
		}
		GameObject go = baseMouseover.go;
		baseMouseover.OnBeforeDestroy();
		_mouseovers.Remove(type);
		Object.Destroy(go);
	}

	public void OnMouseOverOrMove(MouseoverType type, GameObject context, bool forceRefresh = false, Vector2? screenDelta = null)
	{
		Vector2 screenpos = (Vector2)Input.mousePosition / Game.serv.ui.UIScaleFactor;
		if (screenDelta.HasValue)
		{
			screenpos += screenDelta.Value;
		}
		OnMouseOverOrMove(type, screenpos, context, forceRefresh);
	}

	public void OnMouseOverOrMove(MouseoverType type, WorldPos worldpos, GameObject context, bool forceRefresh = false)
	{
		Vector2 screenpos = Game.serv.camera.WorldToScreenPos(worldpos);
		OnMouseOverOrMove(type, screenpos, context, forceRefresh);
	}

	public void OnMouseOverOrMove(MouseoverType type, Vector2 screenpos, GameObject context, bool forceRefresh = false)
	{
		if (!_enabled || !_mouseovers.ContainsKey(type))
		{
			return;
		}
		if (IsShowing && _current != type)
		{
			Hide();
		}
		if (!IsShowing)
		{
			Show(type);
			if (!IsShowing)
			{
				return;
			}
		}
		BaseMouseover baseMouseover = _mouseovers[_current];
		if (baseMouseover == null)
		{
			Logger.Warning("Missing mouseover of type " + _current);
			return;
		}
		if (baseMouseover.context != context || forceRefresh)
		{
			baseMouseover.SetContext(context);
			baseMouseover.RefreshContents();
		}
		baseMouseover.SetScreenPosition(screenpos);
	}

	public void OnMouseOut(MouseoverType type)
	{
		if (_enabled && _current != MouseoverType.None && _current == type)
		{
			Hide();
		}
	}

	public void Refresh(MouseoverType type)
	{
		if (_enabled && _current != MouseoverType.None && _current == type)
		{
			_mouseovers[_current].RefreshContents();
		}
	}

	private void Show(MouseoverType type)
	{
		BaseMouseover baseMouseover = _mouseovers.FindOrNull(type);
		if (baseMouseover == null)
		{
			Logger.Error("Trying to show unregistered mouseover of type: " + type);
			return;
		}
		_current = type;
		PushMouseoversToFront();
		baseMouseover.go.transform.SetAsLastSibling();
		baseMouseover.go.SetActive(value: true);
		baseMouseover.OnShow();
	}

	private void Hide()
	{
		BaseMouseover baseMouseover = _mouseovers.FindOrNull(_current);
		if (baseMouseover == null)
		{
			Logger.Error("Trying to hide non-existent mouseover of type: " + _current);
			_current = MouseoverType.None;
			return;
		}
		baseMouseover.OnHide();
		baseMouseover.SetContext(null);
		baseMouseover.go.SetActive(value: false);
		_current = MouseoverType.None;
	}
}
