using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public static class BuildingPickUtil
{
	public static string MakeMouseover(Entity building, BuildingPickData pickdata)
	{
		string text = BuildingUtil.GenerateBuildingPickMouseover(building);
		if (!pickdata.showpips)
		{
			return text;
		}
		string text2 = BasePickUtil.GeneratePipDescriptions(pickdata.pips);
		return text + "\n\n" + text2;
	}

	public static (bool scoped, string icon) GenerateBuildingButtonIcon(Entity building, bool forceScoped = false)
	{
		bool num = Game.ctx.players.Human.territory.IsScoped(building) || forceScoped;
		bool isAIPlayer = building.components.building.SafehouseOwner.IsAIPlayer;
		string item = "?";
		if (num)
		{
			item = ((!isAIPlayer) ? BuildingUtil.FindBuildingIcon(building) : Loc.Get(SafehouseUtils.CanRaidSafehouse(building, PlayerID.HumanPlayer) ? "ui.icon.scavenge" : "ui.icon.safehouse"));
		}
		return (scoped: num, icon: item);
	}

	public static Color GetPlayerBuildingButtonColor(Entity building, bool scoped, bool crewhere)
	{
		PlayerID controllingPlayer = building.components.building.GetControllingPlayer();
		IRandom identityRNGUnchanging = building.components.ident.GetIdentityRNGUnchanging();
		return GeneratePlayerColor(controllingPlayer, building, scoped, crewhere, identityRNGUnchanging);
	}

	public static Color GenerateNPCColor(Entity npc)
	{
		if (npc == null)
		{
			return Color.black;
		}
		IRandom identityRNGUnchanging = npc.components.ident.GetIdentityRNGUnchanging();
		PlayerID playerID = npc.data.agent?.pid ?? PlayerID.INVALID;
		if (playerID.IsAnyPlayer)
		{
			return playerID.FindPlayer().territory.colorInfo.GetPlayerColor();
		}
		return GeneratePlayerColor(playerID, null, scoped: false, crewhere: false, identityRNGUnchanging);
	}

	public static Color GenerateCornerButtonColor(Node node)
	{
		PlayerID owner = node.owner.Get();
		Xorshift rng = new Xorshift(HashUtil.Hash(node.id.index));
		return GeneratePlayerColor(owner, null, scoped: true, crewhere: false, rng);
	}

	private static Color GeneratePlayerColor(PlayerID owner, Entity building, bool scoped, bool crewhere, IRandom rng)
	{
		if (owner.IsAnyPlayer && scoped)
		{
			return owner.FindPlayer().territory.colorInfo.GetPlayerColor();
		}
		PickColors pickColors = Game.serv.globals.settings.npc.pickColors;
		bool flag = building?.components.residence != null;
		List<PlayerColor> list = ((!scoped) ? pickColors.unknown : ((!crewhere) ? pickColors.toofar : (flag ? pickColors.res : pickColors.biz)));
		return rng.PickElement(list).GetPlayerColor();
	}
}
