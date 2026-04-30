using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Quests;

public class GoalHaveModule : BaseGoal
{
	public List<Label> ids;

	public override string LocDescKey => "goal-have-module.desc";

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override void OnAdded()
	{
		Game.ctx.events.AddListener(SessionEventType.BuildingConstructionStateChanged, OnBuildingConstruction);
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnBuildingConstruction);
		RegisterProgress(CountModules(), startup: true);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.BuildingConstructionStateChanged, OnBuildingConstruction);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnBuildingConstruction);
	}

	private void OnBuildingConstruction(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RegisterProgress(CountModules(), startup: false);
		}
	}

	private void ValidateData()
	{
		foreach (Label id in ids)
		{
			_ = id;
		}
	}

	private int CountModules()
	{
		int num = 0;
		foreach (EntityID item in Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe())
		{
			foreach (Label id in ids)
			{
				if (ModulesUtil.HasModule(item.FindEntity(), id))
				{
					num++;
				}
			}
		}
		return num;
	}

	public override string GetItemName()
	{
		IEnumerable<string> values = (from id in ids
			select ModulesUtil.FindModuleDef(id)?.Common.display.locname into key
			select (key == null) ? "?" : Loc.Get(key)).Deduplicate();
		return string.Join(", ", values);
	}
}
