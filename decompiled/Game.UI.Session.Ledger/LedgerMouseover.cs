using Game.Services;
using Game.UI.Mouseovers;

namespace Game.UI.Session.Ledger;

public class LedgerMouseover : BaseCustomTextMouseover
{
	private string _text = "";

	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.LedgerMouseover;

	public void SetText(bool cansort, string text)
	{
		_text = (cansort ? Loc.Get("ledger.header.mo", "text", text) : text);
		RefreshContents();
	}

	protected override string ProduceText()
	{
		return _text;
	}
}
