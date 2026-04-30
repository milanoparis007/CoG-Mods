using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public abstract class Subview<M, V, C> where M : HUDModel<M, V, C>, new() where V : HUDView<M, V, C>, new() where C : HUDController<M, V, C>, new()
{
	protected GameObject _go;

	protected GameObject panel;

	protected M Model;

	protected V View;

	protected C Controller;

	public virtual bool IsActive => panel.activeSelf;

	protected Subview(GameObject go, string panelName, C controller)
	{
		_go = go;
		panel = go.GetChild(panelName);
		Controller = controller;
		Model = controller.Model;
		View = controller.View;
	}

	public virtual void Activate()
	{
		panel.SetActive(value: true);
	}

	public virtual void Deactivate()
	{
		panel.SetActive(value: false);
	}

	public abstract void RefreshSubview();
}
