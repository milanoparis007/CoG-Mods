using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.Session.Sim.Modules;

public sealed class GamblingModuleConfig : ModuleConfig<GamblingModule, GamblingModuleConfig, GamblingModuleData>
{
	public class Details
	{
		public ModValue aoeRadius = new ModValue();

		public ModValue amenityCount = new ModValue();

		public ModValue regularsGainProbPerTurn = new ModValue();

		public ModValue purchaseCost = new ModValue();

		public ModValue upgradeCost = new ModValue();

		public Label upgradeModule;

		public ConvoButtonRequirementList installReqs = new ConvoButtonRequirementList();

		public ConvoButtonRequirementList installVisReqs = new ConvoButtonRequirementList();

		public List<Label> amenityIds = new List<Label>();
	}

	public Details gambling;

	[Conditional("UNITY_EDITOR")]
	public void DebugValidate()
	{
		GamblingSettings gamblingSettings = Game.serv.globals.settings.gambling;
		foreach (Label amenityId in gambling.amenityIds)
		{
			gamblingSettings.FindAmenityById(amenityId);
		}
	}
}
