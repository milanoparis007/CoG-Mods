namespace Game.Services;

public sealed class TrainSettings
{
	public bool allowRailOverWater;

	public int terminalEdgeMinDistance;

	public int otherTerminalMinDistance;

	public int exitEdgeMaxDistance;

	public int exitLateralMaxDistance;

	public int randomRegionRetries;

	public int randomRailRetries;

	public int railConnectionsRetries;

	public TrainPathSettings path;
}
