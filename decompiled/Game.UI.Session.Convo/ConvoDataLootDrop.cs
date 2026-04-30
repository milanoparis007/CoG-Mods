using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataLootDrop : ConvoData
{
	public string name;

	public string icon;

	public Label resId;

	public Fixnum qty;

	public Fixnum received;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[6]
		{
			"item",
			name,
			"icon",
			icon,
			"received",
			Loc.FormatNumber(received)
		};
	}

	public ConvoDataLootDrop()
	{
	}

	public ConvoDataLootDrop(string name, string icon, Label resId, Fixnum qty)
	{
		this.name = name;
		this.icon = icon;
		this.resId = resId;
		this.qty = qty;
	}
}
