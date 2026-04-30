using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;

namespace Game.Session.Setup;

public sealed class WaterNode
{
	public Label id;

	public WorldPos pos;

	public int width;

	public bool forceFixedWidth;

	public bool body;

	public List<WorldPos> deformers;

	public bool allowBridges;

	public List<Label> connections;

	public WaterNode parent;

	public WaterNode(WaterNodesConfig cfg)
	{
		id = cfg.id;
		pos = cfg.forceStart;
		width = cfg.width;
		forceFixedWidth = cfg.forceFixedWidth;
		body = cfg.body;
		deformers = cfg.deformers;
		allowBridges = cfg.allowBridges;
		connections = cfg.connectingNodes;
	}
}
