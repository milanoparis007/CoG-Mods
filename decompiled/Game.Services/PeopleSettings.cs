using Game.Session.Data;

namespace Game.Services;

public sealed class PeopleSettings : IValidatingSettings
{
	public TraitList traits;

	public SocialSettings social;

	public BuffSettings allBuffs;

	public CombatSettings combatSettings;

	public VehicleSettings vehicleSettings;

	public BuildingSettings buildingSettings;

	public BusinessSettings businessSettings;

	public ResidentialEventSettings residentialEvents;

	public void Validate()
	{
		residentialEvents.Validate();
	}
}
