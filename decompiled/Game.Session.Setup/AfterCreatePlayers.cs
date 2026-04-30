using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Services.Store;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class AfterCreatePlayers
{
	internal void RunBlocking()
	{
		GrantStarterSkill();
		GrantEthStarterSkills();
		GrantMapStarterPack();
		if (Game.serv.store.IsPackInstalled(PackID.ShadowGovernment))
		{
			Game.ctx.simman.politics.EnactStarterPackLaws();
		}
		FixUpSafehouseBiz();
		FixUpVisibility();
		FixUpHomeNodes();
		FixUpMeetingsForHuman();
		ExplorePrecinctsForCops();
		ExploreMapForFeds();
		MaybeProduceDebugDocs();
	}

	private static void GrantStarterSkill()
	{
		Label startingSkill = Game.ctx.scenario.newgamepars.playerdetails.startingSkill;
		if (!startingSkill.IsNotSet)
		{
			SkillDef skill = Game.serv.globals.settings.skills.GetSkill(startingSkill);
			if (skill != null)
			{
				Game.ctx.players.Human.skills.GrantFreebieSkill(skill.id);
			}
		}
	}

	private void GrantEthStarterSkills()
	{
		if (!PlayerCrew.HasEthPackForCurrEth())
		{
			return;
		}
		LabelDictionary<List<Label>> skillStarterPacks = Game.serv.globals.settings.skills.skillStarterPacks;
		LabelDictionary<List<Label>> resourceStarterPacks = Game.serv.globals.settings.skills.resourceStarterPacks;
		List<Label> list = skillStarterPacks.FindOrNull(Game.ctx.players.Human.social.PlayerEthnicity);
		List<Label> list2 = resourceStarterPacks.FindOrNull(Game.ctx.players.Human.social.PlayerEthnicity);
		if (list != null)
		{
			foreach (Label item in list)
			{
				SkillDef skill = Game.serv.globals.settings.skills.GetSkill(item);
				if (skill != null)
				{
					Game.ctx.players.Human.skills.GrantFreebieSkill(skill.id, forceTicker: true);
				}
			}
		}
		if (list2 == null)
		{
			return;
		}
		foreach (Label item2 in list2)
		{
			if (Resource.Find(item2) != null && !Game.ctx.players.Human.skills.HasResourceUnlocked(item2))
			{
				Game.ctx.players.Human.skills.UnlockResource(item2, startup: false);
			}
		}
	}

	private static void GrantMapStarterPack()
	{
		MapConfig mapconfig = Game.ctx.session.mapconfig;
		PlayerInfo human = Game.ctx.players.Human;
		VisitState visit = new VisitState(human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, human.PID);
		switch (mapconfig.playerStart.starterPack)
		{
		case MapConfig.PlayerStartConfig.CityStarterPack.PhiladelphiaStarterPack:
			new GrantRandomCaptain().Apply(new GrantContext(visit));
			break;
		case MapConfig.PlayerStartConfig.CityStarterPack.NewYorkStarterPack:
		{
			Ward ward = human.territory.Safehouse.FindEntity().components.board.GetNode().precinctId.FindWard();
			human.territory.ScopeOutAndMeetOwner(ward.wardOffice.FindEntity(), procgen: true, human.crew.GetCrewForPlayerPeep().peepId, setControlled: false);
			human.social.GetRelationshipFromPlayerTo(ward.currentPolitician).AddBuff(BuffConstants.RELBUFF_NY_POL_STARTER, human.crew.GetCrewForPlayerPeep().peepId);
			human.meetings.MarkNodeAsKnown(ward.wardOffice.FindEntity().components.board.GetNode(), expectedSeen: true, instant: true);
			break;
		}
		case MapConfig.PlayerStartConfig.CityStarterPack.NoStarterPack:
			break;
		}
	}

	private static void FixUpSafehouseBiz()
	{
		Game.ctx.simman.businesses.OnNPCSafehouseCreation();
	}

	private static void FixUpVisibility()
	{
		foreach (TupleStruct<PlayerID, PlayerID> item in FindUnmetPlayers(FindNodesKnownByPlayers()))
		{
			PlayerInfo playerInfo = item.item1.FindPlayer();
			if (!playerInfo.meetings.IsPlayerMet(item.item2))
			{
				playerInfo.meetings.MarkPlayersAsMutuallyMet(item.item2);
			}
		}
	}

	private static List<TupleStruct<PlayerID, PlayerID>> FindUnmetPlayers(Dictionary<PlayerID, List<Node>> nodesKnownByPlayers)
	{
		List<TupleStruct<PlayerID, PlayerID>> list = new List<TupleStruct<PlayerID, PlayerID>>();
		foreach (KeyValuePair<PlayerID, List<Node>> nodesKnownByPlayer in nodesKnownByPlayers)
		{
			PlayerID key = nodesKnownByPlayer.Key;
			foreach (Node item in nodesKnownByPlayer.Value)
			{
				foreach (EntityID item2 in Game.ctx.transit.GetAllAgentsAtNodeUnsafe(item.id))
				{
					if (IsCrewOfUnmetPlayer(item2, key, out var boss))
					{
						list.Add(new TupleStruct<PlayerID, PlayerID>(key, boss));
					}
				}
			}
		}
		return list;
	}

	private static bool IsCrewOfUnmetPlayer(EntityID peep, PlayerID pid, out PlayerID boss)
	{
		boss = peep.FindEntity().data.agent.pid;
		if (boss == pid)
		{
			return false;
		}
		if (!boss.IsAnyPlayer)
		{
			return false;
		}
		return !pid.FindPlayer().meetings.IsPlayerMet(boss);
	}

	private static void FixUpHomeNodes()
	{
		List<PlayerID> list = new List<PlayerID>();
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			list.ClearAndAddRange(item.meetings.GetPlayersAlreadyMet());
			foreach (PlayerID item2 in list)
			{
				Node headquartersNode = item2.FindPlayer().territory.GetHeadquartersNode();
				if (headquartersNode != null && !item.meetings.IsNodeKnown(headquartersNode))
				{
					item.meetings.MarkNodeAsKnown(headquartersNode, expectedSeen: true, instant: false);
				}
			}
		}
	}

	private static void FixUpMeetingsForHuman()
	{
		PlayerInfo human = Game.ctx.players.Human;
		List<Relationship> allPlayerRelationshipsUnsafe = human.social.GetAllPlayerRelationshipsUnsafe();
		IEnumerable<PlayerID> playersAlreadyMet = human.meetings.GetPlayersAlreadyMet();
		foreach (Relationship item in allPlayerRelationshipsUnsafe)
		{
			if (item.IsAnyFamily)
			{
				PlayerID pid = item.to.FindEntity().data.agent.pid;
				if (pid.IsAIPlayer && !human.meetings.IsPlayerMet(pid))
				{
					human.meetings.MarkPlayersAsMutuallyMet(pid, introduceLeadersToCrew: true);
				}
			}
		}
		foreach (PlayerID item2 in playersAlreadyMet)
		{
			PlayerMeetings.IntroducePlayerPeepToOtherCrew(PlayerID.HumanPlayer, item2, force: true);
			PlayerMeetings.IntroducePlayerPeepToOtherCrew(item2, PlayerID.HumanPlayer, force: true);
		}
	}

	private static Dictionary<PlayerID, List<Node>> FindNodesKnownByPlayers()
	{
		Dictionary<PlayerID, List<Node>> dictionary = new Dictionary<PlayerID, List<Node>>(new PlayerIDEqualityComparer());
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			List<PlayerID> data = item.known.data;
			if (data == null || data.Count <= 0)
			{
				continue;
			}
			foreach (PlayerID item2 in data)
			{
				dictionary.AddToList(item2, item);
			}
		}
		return dictionary;
	}

	private static void ExplorePrecinctsForCops()
	{
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (item.IsJustCop)
			{
				ExplorePrecinct(item);
			}
		}
		static void ExplorePrecinct(PlayerInfo cop)
		{
			if (!cop.territory.Station.IsValid)
			{
				return;
			}
			PrecinctID precinctId = cop.territory.GetHeadquartersNode().precinctId;
			foreach (Node item2 in Game.ctx.board.nodes.GetAllNodesUnsafe())
			{
				if (item2.precinctId.Equals(precinctId))
				{
					item2.known.Set(cop.PID, value: true);
				}
			}
		}
	}

	private static void ExploreMapForFeds()
	{
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (item.IsJustFed)
			{
				ExploreMap(item);
			}
		}
		static void ExploreMap(PlayerInfo fed)
		{
			foreach (Node item2 in Game.ctx.board.nodes.GetAllNodesUnsafe())
			{
				item2.known.Set(fed.PID, value: true);
			}
		}
	}

	private static void MaybeProduceDebugDocs()
	{
	}
}
