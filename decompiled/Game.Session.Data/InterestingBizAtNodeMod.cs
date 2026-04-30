using Game.Core;
using Game.Session.Board;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class InterestingBizAtNodeMod : BaseModifier
{
	public bool includeneighbors;

	public Fixnum perbiz = 1;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Node;

	public override string Lockey => "mod.interesting-biz-at-node";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		Node node = query.FindNode();
		if (node == null)
		{
			Logger.Warning("Missing node", this);
			return source;
		}
		int num = node.interesting.Count;
		if (includeneighbors)
		{
			using ListPool<Node>.PooledBlockList pooledBlockList = ListPool<Node>.Allocate();
			node.FindRoadNeighbors(pooledBlockList);
			for (int i = 0; i < pooledBlockList.Count; i++)
			{
				num += pooledBlockList[i].interesting.Count;
			}
		}
		return source + num * perbiz;
	}
}
