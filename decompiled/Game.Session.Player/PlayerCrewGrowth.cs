using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerCrewGrowth
{
	public struct CandidateData
	{
		public EntityID playersFriend;

		public EntityID candidate;

		public RelationshipType relFriendToCandidate;

		public CandidateData(Relationship rel)
		{
			playersFriend = rel.from;
			candidate = rel.to;
			relFriendToCandidate = rel.type;
		}

		public static int CompareByRelationship(CandidateData a, CandidateData b)
		{
			return a.relFriendToCandidate - b.relFriendToCandidate;
		}

		public static int CompareByCandidateAge(CandidateData a, CandidateData b)
		{
			return b.candidate.FindEntity().data.person.born.days - a.candidate.FindEntity().data.person.born.days;
		}
	}

	public Fixnum currentCap;

	public Fixnum lastTurnCap;

	public int currentCrew;

	public int lastTurnCrew;

	public List<CandidateData> humanCrewCandidates;

	public bool isDirty;

	internal int _debugCapModifier;

	private static CrewSettings Settings => Game.serv.globals.settings.people.social.crew;

	internal void DebugApplyCapDelta(int delta)
	{
		_debugCapModifier += delta;
		isDirty = true;
	}

	public bool CapChangedThisTurn()
	{
		return lastTurnCap != currentCap;
	}

	public void OnPlayerTurnStarted(PlayerCrew crew)
	{
		lastTurnCap = currentCap;
		currentCap = Settings.crewSizeCap.Evaluate(new ModQuery(crew.PID)) + _debugCapModifier;
		if (lastTurnCap != currentCap)
		{
			isDirty = true;
		}
		lastTurnCrew = currentCrew;
		currentCrew = crew.TotalCrewCount;
		if (lastTurnCrew != currentCrew)
		{
			isDirty = true;
		}
		if (isDirty)
		{
			isDirty = false;
			RecomputeHumanCrewCandidates(crew);
		}
	}

	public string ExplainCrewCap(PlayerCrew crew, bool includeZeros)
	{
		return Settings.crewSizeCap.Explain(new ModQuery(crew.PID), addHeader: true, includeZeros);
	}

	public int GetVehicleCap(PlayerCrew crew, bool cars)
	{
		return (cars ? Settings.vehicleCarCap : Settings.vehicleTruckCap).Evaluate(crew.PID).IntFloor();
	}

	public string ExplainVehicleCap(PlayerCrew crew, bool cars, bool includeZeros)
	{
		return (cars ? Settings.vehicleCarCap : Settings.vehicleTruckCap).Explain(new ModQuery(crew.PID), addHeader: true, includeZeros);
	}

	public bool HasAnyCrewCandidatesFrom(EntityID playersFriend)
	{
		if (humanCrewCandidates != null)
		{
			foreach (CandidateData humanCrewCandidate in humanCrewCandidates)
			{
				if (humanCrewCandidate.playersFriend == playersFriend)
				{
					return true;
				}
			}
		}
		return false;
	}

	public void GetAllCrewCandidateFrom(EntityID playersFriend, List<CandidateData> results)
	{
		if (humanCrewCandidates == null)
		{
			return;
		}
		foreach (CandidateData humanCrewCandidate in humanCrewCandidates)
		{
			if (humanCrewCandidate.playersFriend == playersFriend)
			{
				results.Add(humanCrewCandidate);
			}
		}
	}

	public EntityID GetBestCrewCandidateFrom(EntityID playersFriend)
	{
		if (humanCrewCandidates == null || humanCrewCandidates.Count == 0)
		{
			return EntityID.INVALID;
		}
		using ListPool<CandidateData>.PooledBlockList pooledBlockList = ListPool<CandidateData>.Allocate();
		GetAllCrewCandidateFrom(playersFriend, pooledBlockList);
		if (pooledBlockList.Count == 0)
		{
			return EntityID.INVALID;
		}
		pooledBlockList.Sort(CandidateData.CompareByCandidateAge);
		return pooledBlockList.FirstOrDefaultFast().candidate;
	}

	public TupleStruct<bool, Fixnum> CheckFriendIntroRequirements(PlayerInfo player, EntityID friend)
	{
		Fixnum familyMinScore = Settings.intros.closeFamilyRel.Evaluate(new ModQuery(player.PID));
		Fixnum otherMinScore = Settings.intros.otherRel.Evaluate(new ModQuery(player.PID));
		return ShouldFriendIntroCrewCandidates(player.social.GetRelationshipFromSourceToPlayer(friend), familyMinScore, otherMinScore);
	}

	private static TupleStruct<bool, Fixnum> ShouldFriendIntroCrewCandidates(Relationship rel, Fixnum familyMinScore, Fixnum otherMinScore)
	{
		if (rel == null)
		{
			return new TupleStruct<bool, Fixnum>(item1: false, otherMinScore);
		}
		Fixnum current = rel.Evaluate().current;
		Fixnum fixnum = (rel.IsCloseFamily ? familyMinScore : otherMinScore);
		return new TupleStruct<bool, Fixnum>(current >= fixnum, fixnum);
	}

	internal void OnSomeoneAddedCrew(PlayerCrew crew, PlayerID _)
	{
		if (crew.PID.IsHumanPlayer)
		{
			RecomputeHumanCrewCandidates(crew);
		}
	}

	private void RecomputeHumanCrewCandidates(PlayerCrew crew)
	{
		if (crew.PID.IsHumanPlayer)
		{
			humanCrewCandidates = humanCrewCandidates ?? new List<CandidateData>();
			humanCrewCandidates.Clear();
			if (crew.CanAddCrew())
			{
				PopulateCrewCandidates(crew.PlayerInfo.social, humanCrewCandidates, Game.ctx.clock.Now);
			}
		}
	}

	private void PopulateCrewCandidates(PlayerSocial social, List<CandidateData> candidates, SimTime now)
	{
		Fixnum familyMinScore = Settings.intros.closeFamilyRel.Evaluate(new ModQuery(social.PID));
		Fixnum otherMinScore = Settings.intros.otherRel.Evaluate(new ModQuery(social.PID));
		foreach (Relationship item in social.GetAllPlayerRelationshipsUnsafe())
		{
			if (ShouldFriendIntroCrewCandidates(social.GetRelationshipFromSourceToPlayer(item.to), familyMinScore, otherMinScore).item1)
			{
				FindAnyCrewCandidates(candidates, item.to, now);
			}
		}
	}

	private void FindAnyCrewCandidates(List<CandidateData> candidates, EntityID other, SimTime now)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(other);
		if (listOrNull == null || listOrNull.data.Count == 0)
		{
			return;
		}
		foreach (Relationship datum in listOrNull.data)
		{
			Entity person = datum.to.FindEntity();
			if (PlayerSocial.IsEligibleCrewMember(now, person))
			{
				candidates.Add(new CandidateData(datum));
			}
		}
	}

	internal string GetCrewCandidateDescription(Entity playersFriend, Entity candidate)
	{
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		PersonInfoUtil.Overview overview = PersonInfoUtil.GenerateOverview(candidate, details: true);
		Relationship orNull = Game.ctx.simman.rels.GetOrNull(playersFriend.Id, candidate.Id);
		string text = Loc.Relationship(orNull?.type ?? RelationshipType.None, candidate.data.person.g);
		string firstName = playersFriend.data.person.FirstName;
		stringBuilder.AppendLine(overview.GetAgeAndEth());
		stringBuilder.AppendLine(Loc.Get("ui.crewmgmt.growth.rel", "friendName", firstName, "relText", text));
		if (orNull != null && orNull.IsAnyFamily)
		{
			stringBuilder.AppendLine(Loc.Get("ui.crewmgmt.growth.is-family"));
		}
		stringBuilder.AppendLine();
		stringBuilder.AppendLine(overview.traitsLong);
		return stringBuilder.ToStringAndReturnToPool().TrimEnd();
	}
}
