using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class PoliticsSettings : IValidatingSettings
{
	public sealed class ElectionSettings
	{
		public int baseSamplingNumber;

		public int nominationStartMonth;

		public int campaignStartMonth;

		public int electionDayStartMonth;

		public int legislationStartMonth;

		public int yearsBetweenElections;

		public int maxCandidatesInElection;

		public int legislationEndMonth;

		public int numLocalPoliticians;

		public int voteCountChunkLow;

		public int donorInfluenceGain;

		public int voteCountChunkHigh;

		public Fixnum influencePer100InWarchestAtWin;

		public Fixnum sponsorInfluenceMultiplier;

		public Fixnum nominatorInfluenceMultiplier;

		public Fixnum supportPricePerVote;

		public Fixnum supportPricePerPositivePolTrait;

		public Fixnum supportPriceForTutorial;

		public ModValue experienceStartupVoteFraction;

		public ModValue influencePerPercentAtWin;

		public ModValue ethnicityStartupVoteFraction;

		public ModValue businessStartupVoteFraction;

		public ModValue traitStartupVoteFraction;

		public ModValue minimumSponsorPrice;

		public ModValue minimumNominatePrice;

		public ModValue warchestPerVote;
	}

	public sealed class CandidateAction
	{
		public bool displayInConvo = true;

		public VisitRequirementList visreqs;

		public ConvoButtonRequirementList reqs;

		public Label id;

		public string locname;

		public string locdesc;

		public string locconvo;

		public Fixnum maxVoteEffect;

		public Fixnum minVoteEffect;

		public Fixnum warchestCost;

		public List<Label> validArchetypes;

		public bool NPCOnly;

		public bool oncePerElection;

		public bool oncePerCandidate;
	}

	public sealed class Law
	{
		public Label id;

		public string locname;

		public string locdesc;

		public VisitRequirementList reqs;

		public VisitRequirementList visreqs;

		public int buyPrice;

		public int revokePrice;

		public ModifierList mods;

		public VisitGrantList grants;

		public string onEnactCallback;

		public string onRevokeCallback;

		public bool expireNextElectionYear;

		public int GetRevokePrice()
		{
			if (revokePrice != 0)
			{
				return revokePrice;
			}
			return buyPrice;
		}
	}

	public sealed class LawStarterPack
	{
		public VisitRequirementList visreqs;

		public int weight;

		public int numLawsToPick;

		public List<Label> possibleLaws;
	}

	public sealed class LawSettings
	{
		public List<Law> lawDefs;

		public List<LawStarterPack> starterPacks;
	}

	public sealed class ActionSettings
	{
		public Fixnum largeActionCooldownWeekz;

		public ModValue largeActionPerformChance;

		public Fixnum smallActionCooldownWeekz;

		public ModValue smallActionPerformChance;

		public List<CandidateAction> largeActionDefs;

		public List<CandidateAction> smallActionDefs;

		public List<CandidateAction> playerActionDefs;
	}

	public sealed class PoliticalArchetype
	{
		public Label id;

		public List<Label> favoredTraits;

		public string locname;

		public string locdesc;

		public Fixnum influenceMultiplier;

		public Fixnum supportPriceMultiplier;
	}

	public ActionSettings npcCandidateAI;

	public ElectionSettings elections;

	public LawSettings lawSettings;

	public List<PoliticalArchetype> archetypeDefs;

	public CandidateAction FindPlayerCandidateAction(Label id)
	{
		foreach (CandidateAction playerActionDef in npcCandidateAI.playerActionDefs)
		{
			if (playerActionDef.id == id)
			{
				return playerActionDef;
			}
		}
		return null;
	}

	public Law FindLawDef(Label id)
	{
		foreach (Law lawDef in lawSettings.lawDefs)
		{
			if (lawDef.id == id)
			{
				return lawDef;
			}
		}
		return null;
	}

	public PoliticalArchetype FindPoliticalArchetype(Label id)
	{
		foreach (PoliticalArchetype archetypeDef in archetypeDefs)
		{
			if (archetypeDef.id == id)
			{
				return archetypeDef;
			}
		}
		return null;
	}

	public void Validate()
	{
		foreach (Law lawDef in lawSettings.lawDefs)
		{
			if (lawDef.buyPrice == 0)
			{
				_ = lawDef.revokePrice;
			}
		}
	}
}
