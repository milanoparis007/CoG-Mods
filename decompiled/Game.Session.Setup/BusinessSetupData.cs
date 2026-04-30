using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Setup;

internal sealed class BusinessSetupData
{
	public struct BizCounts
	{
		public int desiredInteresting;

		public int desiredPotential;
	}

	public sealed class BizEntry
	{
		public EntityConfig config;

		public ZoneType type;

		public Label movesinto;

		public int count;

		public BizEntry(EntityConfig config, ZoneType type, Label movesinto, int count)
		{
			this.config = config;
			this.type = type;
			this.movesinto = movesinto;
			this.count = count;
		}
	}

	public Xorshift rng;

	public int popTotal;

	public int popToAssign;

	public int comTotal;

	public int comToAssign;

	public int indTotal;

	public int indToAssign;

	public Dictionary<NodeID, BizCounts> countsPerNode = new Dictionary<NodeID, BizCounts>(new NodeIDEqualityComparer());

	public List<BizEntry> bizCountsByType = new List<BizEntry>();
}
