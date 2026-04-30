using System;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session.Entities;
using SomaSim.Util;
using TMPro;
using UnityEngine.UI;

namespace Game.UI.Session.Popups;

public sealed class RenamePopup : BasePopup
{
	private const string BUTTON_OK = "Panel/Footer/Ok";

	private const string BUTTON_CANCEL = "Panel/Footer/Cancel";

	private const string PORTRAIT_IMAGE = "Panel/Portrait/Portrait";

	private const string HEADER_TEXT = "Panel/Header";

	private const string DESC_TEXT = "Panel/Description";

	private const string INPUT_FIELD = "Panel/Entry/Input";

	private const string RANDOM_BUTTON = "Panel/Entry/Random Button";

	private EntityID _peepId;

	private string _nick;

	private Action<string> _onOk;

	private Action _onCancel;

	private TMP_InputField _input;

	private Button _button;

	public override UIReference UIReference => UIElements.RenamePopup;

	public RenamePopup(EntityID peepId, Action<string> onOk, Action onCancel)
	{
		_peepId = peepId;
		_onOk = onOk;
		_onCancel = onCancel;
		_nick = Loc.Get("ui.rename.example-nickname");
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
		_go.SetButtonListener("Panel/Entry/Random Button", RandomizeNicknameAndRefresh);
		_input = _go.GetChild<TMP_InputField>("Panel/Entry/Input");
		_input.text = _nick;
		_input.onValueChanged.SetListener(delegate
		{
			OnInputChanged();
		});
		RefreshContents();
	}

	public override void OnDeactivated(bool popped)
	{
		_input.onValueChanged.RemoveAllListeners();
		_go.ClearButtonListeners("Panel/Entry/Random Button");
		_go.ClearButtonListeners("Panel/Footer/Cancel");
		_go.ClearButtonListeners("Panel/Footer/Ok");
		base.OnDeactivated(popped);
	}

	private void OnCancelButton()
	{
		_onCancel();
		Close();
	}

	private void OnOkButton()
	{
		_onOk(_nick);
		Close();
	}

	private void RandomizeNicknameAndRefresh()
	{
		Gender g = _peepId.FindEntity().data.person.g;
		_nick = Loc.GetGendered("ui.rename.random-nickname", g);
		RefreshContents();
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new CapturingKeyboardHandler(() => _input.isFocused, KeyboardHandler.Priority.HighestModalDialog);
	}

	private void RefreshContents()
	{
		Entity entity = _peepId.FindEntity();
		bool num = Game.ctx.players.Human.social.PlayerPeepId == _peepId;
		UIUtil.SetImageOrHide(sprite: HUDUtil.GetCrewSprite(entity), dialog: _go, path: "Panel/Portrait/Portrait");
		string text = Loc.Get("ui.rename.title");
		string text2 = (num ? Loc.Get("ui.rename.header.you") : Loc.GetGendered("ui.rename.header", entity.data.person.g, "name", entity.data.person.FullName));
		_input.SetTextWithoutNotify(_nick);
		_go.SetText("Panel/Header", text + "\n" + text2);
		RefreshFooter();
	}

	private void OnInputChanged()
	{
		RefreshFooter();
	}

	private void RefreshFooter()
	{
		_nick = _input.text.TrimEnd().TrimToLength(30).Sanitize();
		PersonData person = _peepId.FindEntity().data.person;
		string text = _nick + " " + person.last;
		string text2 = person.first + " \"" + _nick + "\" " + person.last;
		string text3 = Loc.Get("ui.rename.describe", "longname", text2, "shortname", text) + "\n" + Loc.Get("ui.rename.footer");
		_go.SetText("Panel/Description", text3);
	}
}
