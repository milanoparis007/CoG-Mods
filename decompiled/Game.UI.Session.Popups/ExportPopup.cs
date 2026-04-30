using Game.Services;
using Game.Services.Input;
using Game.Services.Maps;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Popups;

public sealed class ExportPopup : BasePopup
{
	public const string BUTTON_CLOSE = "Panel/Close";

	public const string BUTTON_CONTINUE = "Panel/Footer/Continue";

	public const string BUTTON_CANCEL = "Panel/Footer/Cancel";

	public const string CITY_IMAGE = "Panel/Info/City Image/Backing/Sprite";

	public const string INPUT_NAME = "Panel/Info/Name/Input";

	public const string INPUT_DESC = "Panel/Info/Desc/Input";

	public const string CHECK_FOLDER = "Panel/Info/Check Folder";

	public const string CHECK_DOCS = "Panel/Info/Check Docs";

	private RawImage _screenshot;

	private TMP_InputField _name;

	private TMP_InputField _desc;

	private Toggle _folder;

	private Toggle _docs;

	public override UIReference UIReference => UIElements.ExportMapPopup;

	protected override void InitializeOnPush()
	{
		_go.SetButtonListener("Panel/Footer/Cancel", OnCancel);
		_go.SetButtonListener("Panel/Close", OnCancel);
		_go.SetButtonListener("Panel/Footer/Continue", OnConfirm);
		_name = _go.GetChild<TMP_InputField>("Panel/Info/Name/Input");
		_desc = _go.GetChild<TMP_InputField>("Panel/Info/Desc/Input");
		_screenshot = _go.GetChild("Panel/Info/City Image/Backing/Sprite").GetComponent<RawImage>();
		_screenshot.texture = new Texture2D(200, 200, TextureFormat.ARGB32, mipChain: false);
		_folder = _go.GetChild<Toggle>("Panel/Info/Check Folder");
		_docs = _go.GetChild<Toggle>("Panel/Info/Check Docs");
		RefreshContents();
	}

	protected override void ReleaseOnPop()
	{
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new CapturingKeyboardHandler(() => _name.isFocused || _desc.isFocused, KeyboardHandler.Priority.HighestModalDialog);
	}

	private void OnCancel()
	{
		Close();
	}

	private void OnConfirm()
	{
		Close();
		Game.serv.mods.CreateNewMapMod(Game.ctx.session.custommap, _name.text, _desc.text, _folder.isOn, _docs.isOn);
		OkPopup.Show(Loc.Get("ui.export.after"));
	}

	private void RefreshContents()
	{
		MapConfig custommap = Game.ctx.session.custommap;
		_name.SetTextWithoutNotify(custommap.CityName);
		_desc.SetTextWithoutNotify(custommap.citydesc ?? "");
		Game.serv.camera.TakeScreenshotAndFillTexture(_screenshot.texture as Texture2D);
	}
}
