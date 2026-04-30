namespace Game.Services.Store;

public class StoreStandalone : NoOpConnector
{
	public override bool IsPackInstalled(PackID id)
	{
		return true;
	}
}
