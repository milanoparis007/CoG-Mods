using System.Collections.Generic;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class CombatAdvisorConfig : AIAdvisorConfig
{
	public sealed class TruceDef
	{
		public ModValue reqAggroDayz;

		public ModValue lengthDayz;

		public ModValue acceptCost;

		public ModValue askForTruceProb;

		public ModValue askForTruceMaxCrewDelta;

		public ModValue askCooldownDayz;

		public ModValue acceptTruceProb;
	}

	public sealed class AggroDef
	{
		public Fixnum hitmanRelDelta;

		public Fixnum compliantRelDelta;

		public ModValue start;

		public ModValue stop;

		public List<PhotoConfig> photos;

		public TruceDef truce;
	}

	public sealed class JointWarDef
	{
		public ModValue reqAggroDayz;

		public ModValue lengthDayz;

		public ModValue acceptCost;

		public ModValue askForJointWarProb;

		public ModValue askCooldownDayz;

		public ModValue acceptJointWarProb;

		public ModValue lockOutRel;

		public ModValue askForJointWarCrewMinimum;
	}

	public AggroDef aggro = new AggroDef();

	public JointWarDef jointWarDef = new JointWarDef();
}
