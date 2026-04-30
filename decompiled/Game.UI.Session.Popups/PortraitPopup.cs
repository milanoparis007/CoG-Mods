using System;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public sealed class PortraitPopup : BasePopup
{
	private const string BUTTON_OK = "Panel/Footer/Ok";

	private const string BUTTON_CANCEL = "Panel/Footer/Cancel";

	private const string PORTRAIT_IMAGE = "Panel/Portrait/Portrait";

	private const string TEXT = "Panel/Text";

	private EntityID _peepId;

	private string _message;

	private Action _onOk;

	private Action _onCancel;

	public override UIReference UIReference => UIElements.PortraitPopup;

	private bool IsCancelButtonVisible => _onCancel != null;

	public PortraitPopup(EntityID peepId, string message, Action onOk = null, Action onCancel = null)
	{
		_peepId = peepId;
		_message = message;
		_onOk = onOk;
		_onCancel = onCancel;
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
		_go.SetButtonListener("Panel/Footer/Ok", OnOkButton);
		_go.SetButtonListener("Panel/Footer/Cancel", OnCancelButton);
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

	private void OnOkButton()
	{
		_onOk?.Invoke();
		Close();
	}

	private void RefreshContents()
	{
		_go.GetChild("Panel/Footer/Cancel").SetActive(IsCancelButtonVisible);
		Entity entity = _peepId.FindEntity();
		_go.GetChild("Panel/Portrait/Portrait").SetActive(entity != null);
		string text = _message;
		if (entity != null)
		{
			text = PersonInfoUtil.GeneratePeepName(entity, showRank: true) + "\n\n" + text;
			Sprite crewSprite = HUDUtil.GetCrewSprite(entity);
			_go.SetImageOrHide("Panel/Portrait/Portrait", crewSprite);
		}
		_go.SetText("Panel/Text", text);
	}
}
