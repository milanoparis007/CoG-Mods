using System;
using Game.Services;
using SomaSim.Util;

namespace Game.UI.Session.Popups;

public abstract class AbstractFancyPopup : BasePopup
{
	protected const string BUTTON_OK = "Panel/Footer/Ok";

	protected const string PORTRAIT_IMAGE = "Panel/Portrait/Portrait";

	protected const string NAME = "Panel/Name";

	protected const string TEXT = "Panel/Text";

	private Action _onOk;

	public override UIReference UIReference => UIElements.NewGameIntroPopup;

	public AbstractFancyPopup(Action onOk)
	{
		_onOk = onOk;
	}

	protected override void InitializeOnPush()
	{
		_go.SetButtonListener("Panel/Footer/Ok", delegate
		{
			Close();
		});
		_go.SetChildText("Panel/Footer/Ok", Loc.Get("button.ok"));
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		RefreshContents();
	}

	public override void OnDeactivated(bool popped)
	{
		if (popped)
		{
			_onOk?.Invoke();
		}
		base.OnDeactivated(popped);
	}

	protected override void ReleaseOnPop()
	{
		_go.ClearButtonListeners("Panel/Footer/Ok");
		_onOk = null;
	}

	protected abstract void RefreshContents();
}
