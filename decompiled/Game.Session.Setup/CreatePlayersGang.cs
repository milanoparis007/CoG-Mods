using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class CreatePlayersGang : CreatePlayers
{
	public CreatePlayersGang(SetupOrchestratorContext ctx)
		: base(ctx)
	{
	}

	internal void RunBlocking()
	{
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (item.IsGangOrGoon)
			{
				SetUpAgent(item);
			}
		}
	}

	private void SetUpAgent(PlayerInfo player)
	{
		Entity entity = FindCandidate(player);
		if (entity == null)
		{
			Logger.Error("NO CREW CANDIDATES for ", player.PID);
		}
		else
		{
			PlayerStartData startingArea = GetStartingArea(_ctx.playerSetup, player, entity);
			CreateCrew(player, startingArea, entity);
			CreatePlayers.CreateSafehouseOrBusiness(player, startingArea, null);
			CreatePlayers.ExploreNodesAroundSafehouse(player, startingArea.node);
		}
	}

	private Entity FindCandidate(PlayerInfo player)
	{
		Xorshift rng = Game.ctx.scenario.MakeSeededRng(player.PID);
		bool goons = player.PlayerType == PlayerType.GoonPlayer;
		return _ctx.playerSetup.GetAndRemoveCandidatePeep(rng, goons);
	}

	private static void CreateCrew(PlayerInfo player, PlayerStartData start, Entity peep)
	{
		CreatePlayers.AddStartupCrewAtNode(player, start.node, peep, isFirstCrewPeep: true);
	}
}
