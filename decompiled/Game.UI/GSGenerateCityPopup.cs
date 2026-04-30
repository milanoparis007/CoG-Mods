using Game.Services;
using SomaSim.Util;

namespace Game.UI;

public class GSGenerateCityPopup : BasePopup
{
	private const string TEXT = "Panel/Text";

	private const string BUGS_PANEL = "Bugs";

	private const string BUGS_TEXT = "Bugs/Text";

	public override UIReference UIReference => UIElements.GeneratingCity;

	public GSGenerateCityPopup()
		: base(hides: false, blurs: false)
	{
	}

	public void SetText(string text)
	{
		_go.SetText("Panel/Text", text);
	}

	protected override void InitializeOnPush()
	{
		bool value = false;
		_go.SetActive("Bugs", value);
		_go.SetText("Bugs/Text", Loc.Get("ui.init.alphabeta"));
	}

	protected override void ReleaseOnPop()
	{
	}
}
