using UnityEngine;

namespace Game.UI.Session.Convo;

public abstract class ViewContents
{
	protected ConversationDialog _dialog;

	protected GameObject _panel;

	public void Initialize(ConversationDialog parent)
	{
		_dialog = parent;
		_panel = CreatePanel(parent.ViewContainer);
		OnAfterInitialize();
		RefreshContents();
	}

	public void Release()
	{
		OnBeforeRelease();
		DestroyPanel();
		_panel = null;
		_dialog = null;
	}

	public abstract GameObject CreatePanel(GameObject container);

	public virtual void DestroyPanel()
	{
		_panel.transform.SetParent(null);
		Object.Destroy(_panel);
	}

	public virtual void OnAfterInitialize()
	{
	}

	public virtual void OnBeforeRelease()
	{
	}

	public virtual void RefreshContents()
	{
	}
}
