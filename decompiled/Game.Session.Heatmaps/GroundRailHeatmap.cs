using Game.Core;
using Game.Session.Board;

namespace Game.Session.Heatmaps;

public class GroundRailHeatmap : Heatmap
{
	public const float NEAR_THRESHOLD = 0.3f;

	public const float VERY_NEAR_THRESHOLD = 0.5f;

	public const float RAIL_DELTA = 0.1f;

	public const float TERMINAL_DELTA = 1f;

	public override HeatmapType Type => HeatmapType.GroundRoadRail;

	public override Config MakeConfig()
	{
		return new Config
		{
			convoKernel = HeatmapConvolutionKernel.GROUNDCOLOR_BLUR,
			updateDuringInteractive = false
		};
	}

	protected override void PopulateHeatmap()
	{
		NodeManager nodes = Game.ctx.board.nodes;
		foreach (Node item in nodes.GetAllNodesUnsafe())
		{
			if (item.HasTerminal)
			{
				AddToHeatmapAroundNode(item.pos, 1f, 1);
			}
		}
		foreach (NodeEdge item2 in nodes.GetAllEdgesUnsafe())
		{
			if (!item2.IsRail)
			{
				continue;
			}
			foreach (RoadBead roadBead in item2.roadBeads)
			{
				AddToHeatmapAroundNode(roadBead.pos, 0.1f, 1);
			}
		}
	}

	public GroundFlags GetGroundFlag(WorldPos pos)
	{
		float valueSafe = GetValueSafe(pos);
		GroundFlags groundFlags = ((valueSafe > 0.3f) ? GroundFlags.NearRoadOrRail : GroundFlags.None);
		GroundFlags groundFlags2 = ((valueSafe > 0.5f) ? GroundFlags.VeryNearRoadOrRail : GroundFlags.None);
		return groundFlags | groundFlags2;
	}
}
