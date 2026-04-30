using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Setup;

namespace Game.Session.Data;

public class AddCrew : VisitGrant
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
		Entity peep = CreatePlayers.CheatGenerateCrewForPlayer(player);
		if (vehTemplate.IsSet)
		{
			player.crew.HireNewCrewInSpecificVehicle(node, peep, null, vehTemplate);
		}
		else
		{
			player.crew.HireNewCrewInVehicle(node, peep, null, isBoss: false);
		}
	}
}
