using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using SomaSim.Util;

namespace Game.Session.Heatmaps;

public class BuildingSizeHeatmap : Heatmap
{
	private Xorshift _rng;

	public override HeatmapType Type => HeatmapType.Buildings;

	public BuildingSizeHeatmap()
	{
		_rng = Game.ctx.scenario.MakeSeededRng<BuildingSizeHeatmap>();
	}

	public override Config MakeConfig()
	{
		return new Config
		{
			clearValue = 0f,
			convoKernel = HeatmapConvolutionKernel.BUILDING_DENSITY_BLUR,
			updateDuringInteractive = false
		};
	}

	protected override void PopulateHeatmap()
	{
		MapConfig.GenClockConfig generator = Game.ctx.session.mapconfig.generator;
		List<Node> allNodesUnsafe = Game.ctx.board.nodes.GetAllNodesUnsafe();
		int count = Game.ctx.session.mapconfig.map.nodes.Count;
		for (int i = 1; i <= count; i++)
		{
			Node node = allNodesUnsafe[i];
			PopulateAtNode(node, generator.nodeCenterScore);
		}
		for (int j = 0; j < generator.otherCenterCount; j++)
		{
			Node node2 = _rng.PickElement(allNodesUnsafe);
			PopulateAtNode(node2, generator.otherCenterScore);
		}
	}

	private void PopulateAtNode(Node node, float value)
	{
		AddToHeatmapAroundNode(node.pos, value);
	}
}
