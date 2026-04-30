using System.Collections.Generic;
using System.Linq;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class CreatePlayersFeds : CreatePlayers
{
	public CreatePlayersFeds(SetupOrchestratorContext ctx)
		: base(ctx)
	{
	}

	internal void RunBlocking()
	{
		foreach (PlayerInfo item in Game.ctx.players.all.Where((PlayerInfo p) => p.IsJustFed).ToList())
		{
			SetUpFedPlayer(item);
		}
	}

	private void SetUpFedPlayer(PlayerInfo player)
	{
		List<Entity> list = (from person in Game.ctx.simman.peoplegen.GetAllTrackedPeople()
			where CreatePlayersCops.EligibleOfficer(Game.ctx.clock.Now, person)
			select person).ToList();
		Entity entity = _ctx.playerSetup.rng.PickElement(list);
		entity.data.person.SetTitle(Loc.Get("ui.name.title-feds"));
		player.crew.AddToCrewUnassigned(entity, null, isBoss: true);
		player.social.SetBossInfo(entity);
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (item.IsJustGang || item.IsHuman)
			{
				player.meetings.MarkPlayersAsMutuallyMet(item.PID);
			}
		}
	}
}
