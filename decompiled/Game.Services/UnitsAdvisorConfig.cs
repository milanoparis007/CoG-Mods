using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.Services;

public sealed class UnitsAdvisorConfig : AIAdvisorConfig
{
	public ModValue cap;

	public ModValue dayzToGrow;

	public ModValue growthChance;

	public Label defaultCar = EntityConstants.VEHICLE_CAR;
}
