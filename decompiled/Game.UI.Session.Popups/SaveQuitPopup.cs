using Game.Services;
using SomaSim.Util;

namespace Game.UI.Session.Popups;

public sealed class SaveQuitPopup : BasePopup
{
	public const string BUTTON_SAVE = "Panel/Save";

	public const string BUTTON_CANCEL = "Panel/Cancel";

	public const string BUTTON_QUIT = "Panel/Footer/Quit";

	public const string BUTTON_SETTINGS = "Panel/Footer/Settings";

	public const string BUTTON_EXPORT = "Panel/Footer/Export";

	public override UIReference UIReference => UIElements.SaveQuitPopup;

	protected override void InitializeOnPush()
	{
		_go.SetButtonListener("Panel/Save", OnSave);
		_go.SetButtonListener("Panel/Footer/Quit", OnQuit);
		_go.SetButtonListener("Panel/Footer/Settings", OnSettings);
		_go.SetButtonListener("Panel/Footer/Export", OnExport);
		_go.SetButtonListener("Panel/Cancel", Close);
		_go.SetActive("Panel/Footer/Export", Game.serv.mods.IsModdingEnabled);
		_go.GetButton("Panel/Footer/Export").interactable = Game.ctx.session.custommap.IsProcGen;
		_go.GetButton("Panel/Save").interactable = Game.ctx.CanAutosaveGame;
	}

	protected override void ReleaseOnPop()
	{
	}

	private void OnSave()
	{
		Close();
		Game.ctx.ShowSaveDialog();
	}

	private void OnQuit()
	{
		Close();
		OkPopup.ShowOkCancel(Loc.Get("ui.quitmenu.quit.confirm"), delegate
		{
			TimerUtil.RunNextFrame(Game.ctx.QuitGame);
		}, delegate
		{
		});
	}

	private void OnSettings()
	{
		Game.serv.ui.AddPopup(new OptionsPopup());
	}

	private void OnExport()
	{
		Game.serv.ui.AddPopup(new ExportPopup());
	}
}
