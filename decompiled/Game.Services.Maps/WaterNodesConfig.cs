using System.Collections.Generic;
using Game.Core;

namespace Game.Services.Maps;

public sealed class WaterNodesConfig
{
	public Label id;

	public WorldPos forceStart = WorldPos.Zero;

	public bool forceFixedWidth;

	public bool body;

	public List<WorldPos> deformers;

	public bool allowBridges = true;

	public int width;

	public List<Label> connectingNodes = new List<Label>();
}
