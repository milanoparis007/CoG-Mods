using System;
using Game.Services;
using Game.Services.Audio;
using Game.Session.Assets;
using SomaSim.Util;

namespace Game.UI.Session.Popups;

public sealed class OkCancelPopup : BasePopup
{
	private const string BUTTON_OK = "Panel/Footer/Ok";

	private const string BUTTON_CANCEL = "Panel/Footer/Cancel";

	private const string TEXT_OK = "Panel/Footer/Ok/Text";

	private const string TEXT_CANCEL = "Panel/Footer/Cancel/Text";

	private const string BODY_TEXT = "Text";

	private string _message;

	private string _okText = Loc.Get("button.ok");

	private string _cancelText = Loc.Get("button.cancel");

	private Action _onOk;

	private Action _onCancel;

	private bool _okEnabled = true;

	public override UIReference UIReference => UIElements.OkCancelPopup;

	private bool IsCancelButtonVisible => _onCancel != null;

	public OkCancelPopup(string message, Action onOk = null, Action onCancel = null, bool hides = false)
		: base(hides, blurs: true)
	{
		_message = message;
		_onOk = onOk;
		_onCancel = onCancel;
	}

	public OkCancelPopup(string message, string okText, string cancelText, Action onOk = null, Action onCancel = null, bool okEnabled = true)
	{
		_message = message;
		_onOk = onOk;
		_onCancel = onCancel;
		_okText = okText;
		_cancelText = cancelText;
		_okEnabled = okEnabled;
	}

	protected override void InitializeOnPush()
	{
	}

	protected override void ReleaseOnPop()
	{
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		_go.SetText("Panel/Footer/Ok/Text", _okText);
		_go.SetText("Panel/Footer/Cancel/Text", _cancelText);
		_go.SetButtonListener("Panel/Footer/Ok", OnOKButton);
		_go.GetButton("Panel/Footer/Ok").interactable = _okEnabled;
		_go.SetButtonListener("Panel/Footer/Cancel", OnCancelButton);
		PlayUISound.UpdateEffectOn(_go, "Panel/Footer/Ok", SFXType.ClickButtonConfirm);
		RefreshContents();
	}

	public override void OnDeactivated(bool popped)
	{
		_go.ClearButtonListeners("Panel/Footer/Cancel");
		_go.ClearButtonListeners("Panel/Footer/Ok");
		base.OnDeactivated(popped);
	}

	private void OnCancelButton()
	{
		_onCancel?.Invoke();
		Close();
	}

	private void OnOKButton()
	{
		_onOk?.Invoke();
		Close();
	}

	private void RefreshContents()
	{
		_go.GetChild("Panel/Footer/Cancel").SetActive(IsCancelButtonVisible);
		_panel.SetText("Text", _message);
	}
}
