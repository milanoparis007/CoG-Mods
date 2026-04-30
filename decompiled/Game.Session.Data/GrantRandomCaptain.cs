using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Setup;
using SomaSim.Util;

namespace Game.Session.Data;

public class GrantRandomCaptain : VisitGrant
{
	public enum Target
	{
		Safehouse,
		Tag
	}

	public Target at;

	public Label buildingTag;

	public Label vehTemplate;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		Entity entity = ((at == Target.Safehouse) ? player.territory.Safehouse.FindEntity() : player.territory.FindFirstControlledBuildingWithTag(buildingTag));
		if (entity == null)
		{
			entity = player.territory.Safehouse.FindEntity();
		}
		Node node = entity.components.board.GetNode();
		Entity entity2 = CreatePlayers.CheatGenerateCrewForPlayer(player);
		if (vehTemplate.IsSet)
		{
			player.crew.HireNewCrewInSpecificVehicle(node, entity2, null, vehTemplate);
		}
		else
		{
			player.crew.HireNewCrewInVehicle(node, entity2, null, isBoss: false);
		}
		entity2.components.agent.AddXP(800);
		XP xp = entity2.data.agent.xp;
		List<LevelupDescription> list = entity2.components.agent.GetAvailableLevelups(explain: false).ToList();
		for (int i = 0; i < 4; i++)
		{
			LevelupDescription levelupDescription = entity2.data.ident.rng.PickElement(list);
			xp.GetLevelupLevel(levelupDescription.levelup.id);
			xp.lastThreshold++;
			xp.SetLevelupLevel(levelupDescription.levelup.id, levelupDescription.nextLevel);
			list = entity2.components.agent.GetAvailableLevelups(explain: false).ToList();
		}
		Label id = Game.serv.globals.settings.skills.experience.GetCaptainLevelup().id;
		xp.lastThreshold++;
		xp.SetLevelupLevel(id, 1);
	}
}
