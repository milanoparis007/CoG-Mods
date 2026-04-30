using Game.Core;
using Game.Session.Board;

namespace Game.Session.Heatmaps;

public class RailPlacementHeatmap : Heatmap
{
	public const float RAIL_DELTA = 0.03f;

	public const float TERMINAL_DELTA = 1f;

	public override HeatmapType Type => HeatmapType.Rail;

	public override Config MakeConfig()
	{
		return new Config
		{
			clearValue = 0.01f,
			convoKernel = HeatmapConvolutionKernel.PLACEMENT_BLUR
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
				AddToHeatmapAroundNode(roadBead.pos, 0.03f, 1);
			}
		}
	}
}
