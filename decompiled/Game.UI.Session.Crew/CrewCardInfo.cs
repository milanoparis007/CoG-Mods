using System;
using System.Diagnostics;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.UI.Session.Crew;

[DebuggerDisplay("{DebugString}")]
public sealed class CrewCardInfo
{
	public static readonly CrewCardType[] ALL_TYPES = Enum.GetValues(typeof(CrewCardType)) as CrewCardType[];

	public readonly CrewCardType type;

	public readonly CrewAssignment crew;

	public readonly EntityID building;

	public readonly EntityID emptyVehicle;

	public readonly AutomationID automation;

	public readonly CrewInfoGen gen;

	private string DebugString => $"[{type} crew:{crew} b:{building} v:{emptyVehicle} auto:{automation}]";

	public CrewCardInfo(CrewCardType type, CrewAssignment? crew = null, EntityID? building = null, EntityID? emptyVehicle = null, AutomationID? automation = null)
	{
		this.type = type;
		this.crew = crew ?? CrewAssignment.EMPTY;
		this.building = building ?? EntityID.INVALID;
		this.emptyVehicle = emptyVehicle ?? EntityID.INVALID;
		this.automation = automation ?? AutomationID.INVALID;
		gen = type switch
		{
			CrewCardType.VehicleUnassigned => new CrewInfoGenJustVehicle(this), 
			CrewCardType.CrewScheming => new CrewInfoGenScheming(this), 
			CrewCardType.CrewUnassigned => new CrewInfoGenJustPeep(this), 
			CrewCardType.CrewJobs => new CrewInfoGenJob(this), 
			CrewCardType.CrewManagerOrBuilding => new CrewInfoGenBuilding(this), 
			CrewCardType.CrewMuscle => new CrewInfoGenMuscle(this), 
			_ => null, 
		};
	}

	public CrewCardInfo(CrewCardInfoInitData data)
		: this(data.type, data.crew, data.building, data.emptyVehicle, data.automation)
	{
	}

	internal Entity FindSelectionTargetOnBoard()
	{
		return building.FindEntity() ?? emptyVehicle.FindEntity() ?? crew.GetTarget();
	}

	public override string ToString()
	{
		return DebugString;
	}
}
