using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerSchemeData
{
	public Xorshift rng = new Xorshift(1u);

	public List<Label> schemesAvailable = new List<Label>();

	public List<SchemeData> ongoingSchemes = new List<SchemeData>();

	public List<Label> schemeHistory = new List<Label>();

	public List<HistoryItem> chapterHistory = new List<HistoryItem>();

	public List<SchemeCooldown> cooldowns = new List<SchemeCooldown>();

	public PlayerSchemeData()
	{
	}

	public PlayerSchemeData(PlayerID pid)
	{
		rng = Game.ctx.scenario.MakeSeededRng(pid);
	}

	public SchemeData GetOngoingSchemeForId(Label schemeID)
	{
		foreach (SchemeData ongoingScheme in ongoingSchemes)
		{
			if (ongoingScheme.schemeID == schemeID)
			{
				return ongoingScheme;
			}
		}
		return null;
	}
}
