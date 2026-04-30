using System.Collections.Generic;
using Game.Core;

namespace Game.Services.Maps;

public sealed class MapNodesConfig
{
	public Label id;

	public IntSize nodeSpacing;

	public float relativeSpeed = 1f;

	public float railCost = 1f;

	public bool dontSkipRoads;

	public bool allowAsIsland;

	public bool stopAtWater;

	public bool forceBridges;

	public bool connection;

	public bool boardwalk;

	public WorldPos forceStart = WorldPos.Zero;

	public float forceAngle = float.MaxValue;

	public int forceMaxSize = int.MaxValue;

	public IntSize forceMaxSpread = new IntSize(int.MaxValue, int.MaxValue);

	public List<Label> initialNames = new List<Label>();

	public bool HasForcedAngle => forceAngle != float.MaxValue;

	public bool HasForceMaxSize => forceMaxSize != int.MaxValue;
}
