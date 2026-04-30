using System.Collections.Generic;
using Game.Core;
using Game.Session.Player.KB;
using UnityEngine;

namespace Game.UI.Session.Picks;

public struct CornerPickData
{
	public List<KBResult> pips;

	public bool showpips;

	public Color color;

	public PlayerID owner;

	public static CornerPickData GenerateCornerButtonData(Node node)
	{
		PlayerKB kb = Game.ctx.players.Human.kb;
		CornerPickData result = default(CornerPickData);
		Color color = BuildingPickUtil.GenerateCornerButtonColor(node);
		result.color = color;
		result.owner = node.owner.pid;
		result.pips = new List<KBResult>
		{
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_TERRITORY_STOLEN),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_TERRITORY_CONTRACTED),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_TERRITORY_EXPANDED),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_TERRITORY_PUMPING),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_TERRITORY_OWNED),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_HEAT_COPS_SOON),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_HEAT_COPS_RECENT),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_HEAT_UP),
			kb.GetStatusForCorner(node, PlayerKBQueryNames.CORNER_HEAT_DOWN)
		};
		result.showpips = false;
		for (int i = 0; i < result.pips.Count; i++)
		{
			if (result.pips[i].showpip)
			{
				result.showpips = true;
				break;
			}
		}
		return result;
	}
}
