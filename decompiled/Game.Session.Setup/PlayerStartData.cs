using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Setup;

public class PlayerStartData
{
	public Node node;

	public EntityConfig frontBiz;

	public bool InstallSafeHouseInBusiness => frontBiz != null;

	public bool InstallSafeHouseAnywhere => frontBiz == null;
}
