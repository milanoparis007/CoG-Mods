using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Player;

public static class SocQ
{
	public delegate bool TernaryPredicate(Entity player, Entity other, Entity candidate);

	public delegate float TernaryKeySelector(Entity player, Entity other, Entity candidate);

	public static IEnumerable<Entity> FindPeople(Entity player, Entity source, TernaryPredicate test)
	{
		return from candidate in GetRelations(source.Id)
			where test(player, source, candidate)
			select candidate;
	}

	public static IEnumerable<Entity> OrderBy(IEnumerable<Entity> collection, Entity player, Entity source, TernaryKeySelector fn)
	{
		return collection.OrderBy((Entity candidate) => fn(player, source, candidate));
	}

	public static IEnumerable<Entity> OrderByDescending(IEnumerable<Entity> collection, Entity player, Entity source, TernaryKeySelector fn)
	{
		return collection.OrderByDescending((Entity candidate) => fn(player, source, candidate));
	}

	public static bool IsNotKnownToPlayer(Entity player, Entity _, Entity candidate)
	{
		return Game.ctx.simman.rels.HasNone(player.Id, candidate.Id);
	}

	public static bool IsKnownToPlayerAndOther(Entity player, Entity other, Entity candidate)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		if (player != candidate && other != candidate && rels.HasAny(candidate.Id, player.Id))
		{
			return rels.HasAny(candidate.Id, other.Id);
		}
		return false;
	}

	public static float DistanceToPlayer(Entity player, Entity _, Entity candidate)
	{
		WorldPos? worldPos = BuildingUtil.FindBuildingForBizOwner(candidate)?.data.board.worldpos;
		if (!worldPos.HasValue)
		{
			return float.PositiveInfinity;
		}
		WorldPos? worldPos2 = player.data.agent.pid.FindPlayer()?.territory.GetHeadquartersNode()?.pos;
		if (!worldPos2.HasValue)
		{
			return float.PositiveInfinity;
		}
		return (worldPos.Value - worldPos2.Value).Magnitude;
	}

	public static float RelationshipToPlayer(Entity player, Entity _, Entity candidate)
	{
		return (float)Game.ctx.simman.rels.GetOrNull(candidate.Id, player.Id).Evaluate().current;
	}

	private static IEnumerable<Entity> GetRelations(EntityID sourceId)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(sourceId);
		if (listOrNull == null)
		{
			yield break;
		}
		foreach (Relationship datum in listOrNull.data)
		{
			yield return datum.to.FindEntity();
		}
	}
}
