namespace Game.Core;

public sealed class GameAnimUpdate
{
	public float frameDeltaSeconds;

	public float cumulativeSeconds;

	public bool IsAdvancing => frameDeltaSeconds > 0f;
}
