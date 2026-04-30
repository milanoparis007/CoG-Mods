using System.Collections.Generic;
using Game.Session.Data;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public sealed class VehicleSettings
{
	public sealed class HealthInfo
	{
		public VehicleHealthType category;

		public int morethan;

		public bool canwork;

		public bool showbar;

		public Color32 color;

		public bool IsGood => category == VehicleHealthType.Good;

		public bool IsWorn => category == VehicleHealthType.Worn;

		public bool IsClunker => category == VehicleHealthType.Clunker;

		public bool IsJunk => category == VehicleHealthType.Junk;
	}

	public sealed class HealthLossInfo
	{
		public VehicleHealthSource source;

		public ModValue points;
	}

	public ModValue maxVehicleHealth;

	public List<HealthInfo> healthTypes;

	public List<HealthLossInfo> healthLoss;

	public HealthInfo FindHealthInfo(Fixnum health)
	{
		int i = 0;
		for (int count = healthTypes.Count; i < count; i++)
		{
			HealthInfo healthInfo = healthTypes[i];
			if (health > healthInfo.morethan)
			{
				return healthInfo;
			}
		}
		return healthTypes.LastOrDefaultFast();
	}

	public HealthLossInfo FindHealthLossInfo(VehicleHealthSource source)
	{
		int i = 0;
		for (int count = healthLoss.Count; i < count; i++)
		{
			HealthLossInfo healthLossInfo = healthLoss[i];
			if (healthLossInfo.source == source)
			{
				return healthLossInfo;
			}
		}
		return null;
	}
}
