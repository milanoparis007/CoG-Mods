using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class AttackAdvisorConfig : AIAdvisorConfig
{
	public class GenericAttack
	{
		public ModValue maxDistance;

		public ModValue minCrewSize;

		public ModValue maxRel;

		public ModValue firstCheckDayz;

		public ModValue checkProbability;

		public ModValue checkDayz;
	}

	public class OpportunisticAttack
	{
		public ModValue minHealth;

		public ModValue maxDistance;
	}

	public class CoordinatedAttack
	{
		public ModValue maxDistance;

		public ModValue minCrewSize;

		public ModValue maxRel;

		public ModValue minHealth;

		public ModValue firstCheckDayz;

		public ModValue checkProbability;

		public ModValue checkDayz;
	}

	public LabelDictionary<FloatMapper> weaponProbs = new LabelDictionary<FloatMapper>();

	public GenericAttack buildingAttack;

	public GenericAttack forcedClosure;

	public GenericAttack sellOutToFeds;

	public GenericAttack casinoAttack;

	public OpportunisticAttack opportunisticAttack;

	public CoordinatedAttack coordinatedAttack;

	public IEnumerable<Label> GetWeaponDropsAtCurrentTime(IRandom rng)
	{
		float x = Game.ctx.clock.GetTimeSinceProcGen().YearsFloat;
		foreach (KeyValuePair<Label, FloatMapper> weaponProb in weaponProbs)
		{
			float probability = weaponProb.Value.Eval(x);
			if (rng.CheckAndReturn(probability).pass)
			{
				yield return weaponProb.Key;
			}
		}
	}
}
