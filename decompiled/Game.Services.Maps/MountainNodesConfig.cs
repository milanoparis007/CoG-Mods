using Game.Core;

namespace Game.Services.Maps;

public sealed class MountainNodesConfig
{
	public Label id;

	public WorldPos forceStart = WorldPos.Zero;

	public float size;

	public float minHeight;

	public float maxHeight;
}
