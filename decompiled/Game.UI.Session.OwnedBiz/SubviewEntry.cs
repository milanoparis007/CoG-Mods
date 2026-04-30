using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

internal abstract class SubviewEntry
{
	internal ViewType type;

	internal GameObject go;

	protected OwnedBizController Controller;

	protected OwnedBizDialog Dialog;

	protected OwnedBizModel Model;

	public SubviewEntry(ViewType type, GameObject go)
	{
		this.type = type;
		this.go = go;
	}

	public virtual void Initialize(OwnedBizController controller)
	{
		Controller = controller;
		Model = controller.Model;
		Dialog = controller.View;
	}

	public virtual void Release()
	{
		Dialog = null;
		Controller = null;
		Model = null;
		type = ViewType.None;
		go = null;
	}

	public virtual void OnActivated()
	{
		go.ResetAllChildScrollViews();
		RefreshSubview();
	}

	public virtual void OnDeactivated()
	{
	}

	public abstract void RefreshSubview();
}
