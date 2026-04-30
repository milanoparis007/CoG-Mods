using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public sealed class CombatSettings
{
	public sealed class HealthInfo
	{
		public int morethan;

		public int category;

		public string locname;

		public string lochurt;

		public string locunhurt;

		public bool canwork;

		public Color32 color;
	}

	public sealed class CrewDeath
	{
		public Label supportSocial;

		public Label questSocial;

		public Label ignoreSocial;

		public List<Label> questName;
	}

	public ModValue maxCrewHealth;

	public ModValue crewHealingPoints;

	public ModValue vehicleDamageReduction;

	public List<HealthInfo> crewHealthTypes;

	public CrewDeath crewDeath;

	public List<PhotoConfig> eliminateGangPhotos;

	public HealthInfo FindCrewHealthInfo(Fixnum health)
	{
		int i = 0;
		for (int count = crewHealthTypes.Count; i < count; i++)
		{
			HealthInfo healthInfo = crewHealthTypes[i];
			if (health > healthInfo.morethan)
			{
				return healthInfo;
			}
		}
		return crewHealthTypes.LastOrDefaultFast();
	}
}
