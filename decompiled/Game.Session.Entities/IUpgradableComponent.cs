using Game.Core;

namespace Game.Session.Entities;

public interface IUpgradableComponent
{
	object GenerateUpgradeData();

	void ConsumeUpgradeData(EntityID previousId, object data);

	void OnAfterUpgrade(EntityID previousId);
}
