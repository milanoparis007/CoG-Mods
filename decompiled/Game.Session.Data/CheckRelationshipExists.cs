using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckRelationshipExists : AbstractVisitRequirement
{
	public enum From
	{
		Player,
		Crew
	}

	public enum OfType
	{
		Family,
		CloseFamily,
		Acquaintance,
		Any
	}

	public From from;

	public OfType oftype;

	public bool expected = true;

	public override bool DoesPass(VisitState visit)
	{
		return FindMatch(visit).IsValid == expected;
	}

	private EntityID FindMatch(VisitState visit)
	{
		PlayerInfo player = GetPlayer(visit);
		EntityID id = visit.npc.Id;
		switch (from)
		{
		case From.Crew:
			return CheckCrew(player, id);
		case From.Player:
			if (!CheckIndividual(player.social.PlayerPeepId, id))
			{
				return EntityID.INVALID;
			}
			return player.social.PlayerPeepId;
		default:
			return EntityID.INVALID;
		}
	}

	private EntityID CheckCrew(PlayerInfo player, EntityID owner)
	{
		foreach (CrewAssignment item in player.crew.AllCrew)
		{
			if (!item.IsDead && CheckIndividual(item.peepId, owner))
			{
				return item.peepId;
			}
		}
		return EntityID.INVALID;
	}

	private bool CheckIndividual(EntityID crew, EntityID owner)
	{
		Relationship orNull = Game.ctx.simman.rels.GetOrNull(owner, crew);
		if (orNull == null)
		{
			return false;
		}
		bool result = false;
		switch (oftype)
		{
		case OfType.Family:
			result = orNull.IsAnyFamily;
			break;
		case OfType.CloseFamily:
			result = orNull.IsCloseFamily;
			break;
		case OfType.Acquaintance:
			result = !orNull.IsAnyFamily && !orNull.IsSelf;
			break;
		case OfType.Any:
			result = !orNull.IsSelf;
			break;
		}
		return result;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.biz.intro"));
	}
}
