using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public sealed class BuildingSettings
{
	public sealed class HealthInfo
	{
		public int morethan;

		public bool showbar;

		public Color32 color;
	}

	public ModValue ctrlMaxBuildingHealth;

	public ModValue ctrlHealingPoints;

	public ModValue ctrlAttackPoints;

	public List<HealthInfo> healthTypes;

	public List<PhotoConfig> humanAttackedPhotos;

	public List<PhotoConfig> humanDestroyedPhotos;

	public List<PhotoConfig> gangAttackedPhotos;

	public List<PhotoConfig> gangDestroyedPhotos;

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

	public static Fixnum EvaluateCtrl(ModValue modvalue, Entity building, PlayerID inquirer)
	{
		EntityID targetId = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building)?.Id ?? EntityID.INVALID;
		NodeID nodeID = building.components.board.GetNodeID();
		ModQuery query = new ModQuery(inquirer, targetId, nodeID);
		return modvalue.Evaluate(query);
	}

	public static Fixnum EvaluateMaxBuildingHealth(Entity building, PlayerID inquirer)
	{
		return EvaluateCtrl(Game.serv.globals.settings.people.buildingSettings.ctrlMaxBuildingHealth, building, inquirer);
	}

	public static Fixnum EvaluateHealingPoints(Entity building, PlayerID inquirer)
	{
		return EvaluateCtrl(Game.serv.globals.settings.people.buildingSettings.ctrlHealingPoints, building, inquirer);
	}

	public static Fixnum EvaluateAttackPoints(Entity building, PlayerID inquirer)
	{
		return EvaluateCtrl(Game.serv.globals.settings.people.buildingSettings.ctrlAttackPoints, building, inquirer);
	}
}
