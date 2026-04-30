using System.Collections.Generic;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class PoliceSettings
{
	public sealed class Feds
	{
		public ModValue hintCooldownDayz;

		public ModValue arrestDayz;

		public ModValue arrestBribe;

		public ModValue trialReleaseProbHuman;

		public ModValue trialReleaseProbGang;

		public ModValue prisonTimeYears;

		public List<PhotoConfig> raidPhotos;

		public List<PhotoConfig> arrestPhotos;

		public List<PhotoConfig> trialPhotos;
	}

	public ModValue raidThreshold;

	public ModValue raidChance;

	public ModValue raidCheckCooldownDayz;

	public ModValue raidNodeCooldownDayz;

	public IntRange raidDurationDayz;

	public float premium;

	public ModValue donationCost;

	public ModValue donationDayz;

	public ModValue donationFromPolDayz;

	public Feds feds;
}
