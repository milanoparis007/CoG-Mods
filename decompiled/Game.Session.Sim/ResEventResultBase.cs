using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public abstract class ResEventResultBase : ResidentialEventResultConfig
{
	protected void PopulateEventMessage(ResEventResultData data)
	{
		Entity entity = data.targetNpc.FindEntity();
		string fullName = entity.data.person.FullName;
		string gendered = Loc.GetGendered(loceventresult, entity.data.person.g, "othername", fullName);
		data.eventResultMessage = gendered;
	}

	protected List<Entity> FindHostConnectionsUnknown(VisitState visit)
	{
		Entity playerPeep = visit.GetPlayer().social.GetPlayerPeep();
		return SocQ.OrderBy(from peep in SocQ.FindPeople(playerPeep, visit.npc, SocQ.IsNotKnownToPlayer)
			where peep.data.person.IsEmployed && IsValidBizBuilding(BuildingUtil.FindBuildingForBizOwner(peep))
			select peep, playerPeep, visit.npc, SocQ.DistanceToPlayer).ToList();
	}

	private bool IsValidBizBuilding(Entity building)
	{
		if (building == null || building.data.building == null)
		{
			return false;
		}
		BuildingData building2 = building.data.building;
		if (building2.interesting)
		{
			return building2.business.IsValid;
		}
		return false;
	}

	protected List<Entity> FindHostConnectionsForQuest(VisitState visit)
	{
		Entity playerPeep = visit.GetPlayer().social.GetPlayerPeep();
		return SocQ.OrderBy(SocQ.FindPeople(playerPeep, visit.npc, (Entity player, Entity _, Entity candidate) => (Game.ctx.simman.rels.GetOrNull(candidate.Id, player.Id)?.Evaluate().current ?? ((Fixnum)0)) >= 0).Where(delegate(Entity peep)
		{
			Entity entity = BuildingUtil.FindBuildingForBizOwner(peep);
			if (!IsValidBizBuilding(entity))
			{
				return false;
			}
			if (entity.components.board.GetNode().owner.pid.IsAIPlayer)
			{
				return false;
			}
			return !Game.ctx.quests.FindActiveOrWaitingQuestForTarget(peep.Id).IsSet;
		}), playerPeep, visit.npc, SocQ.DistanceToPlayer).ToList();
	}

	private List<Entity> FindUnknownBizBuildingsByDistance(VisitState visit)
	{
		WorldPos currentPos = visit.building.data.board.worldpos;
		return (from b in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe()
			where IsValidBizBuilding(b) && !b.components.building.IsScopedBy(PlayerID.HumanPlayer)
			orderby (b.data.board.worldpos - currentPos).MagnitudeSquared
			select b).ToList();
	}

	protected List<Entity> FindUnknownBizOwnersByDistance(VisitState visit)
	{
		List<Entity> source = FindUnknownBizBuildingsByDistance(visit);
		Entity playerPeep = visit.GetPlayer().social.GetPlayerPeep();
		return (from b in source
			select BuildingUtil.FindOwnerOrManagerForAnyBuilding(b) into owner
			where owner != null && SocQ.IsNotKnownToPlayer(playerPeep, null, owner)
			select owner).ToList();
	}
}
