namespace Game.Services;

public sealed class TrainPathSettings
{
	public float defaultCost;

	public float unitDistanceCost;

	public float sharedTrackCostMultiplier;

	public float edgeTrackCostMultiplier;

	public float headingChangePenalty;

	public int expensiveEdgeWidth;
}
