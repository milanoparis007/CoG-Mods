using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class CrewSettings
{
	public sealed class CrewIntrosSettings
	{
		public ModValue closeFamilyRel;

		public ModValue otherRel;
	}

	public sealed class CrewRoleSettings
	{
		public List<RoleDef> roleDefs = new List<RoleDef>();

		public RoleDef GetRoleById(Label id)
		{
			foreach (RoleDef roleDef in roleDefs)
			{
				if (roleDef.id == id)
				{
					return roleDef;
				}
			}
			return null;
		}
	}

	public sealed class StatCategory
	{
		public string locname;

		public CrewStatCategory catEnum;

		public List<CrewStats> entries = new List<CrewStats>();
	}

	public ModValue turnCostBoss;

	public ModValue turnCostCrew;

	public ModValue turnCostDead;

	public ModValue crewSizeCap;

	public ModValue vehicleCarCap;

	public ModValue vehicleTruckCap;

	public CrewIntrosSettings intros;

	public CrewRoleSettings roleSettings;

	public List<StatCategory> statCategories;
}
