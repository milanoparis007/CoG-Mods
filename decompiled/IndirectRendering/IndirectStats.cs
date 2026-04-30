using System.Collections.Generic;

namespace IndirectRendering;

public class IndirectStats
{
	public string name;

	public uint totalInstances;

	public List<LODStats> stats = new List<LODStats>();

	public uint TotalVisibleInstances
	{
		get
		{
			uint total = 0u;
			stats.ForEach(delegate(LODStats stat)
			{
				total += stat.visibleInstanceCount;
			});
			return total;
		}
	}

	public uint TotalVerts
	{
		get
		{
			uint total = 0u;
			stats.ForEach(delegate(LODStats stat)
			{
				total += stat.totalVerts;
			});
			return total;
		}
	}

	public uint TotalIndices
	{
		get
		{
			uint total = 0u;
			stats.ForEach(delegate(LODStats stat)
			{
				total += stat.totalIndices;
			});
			return total;
		}
	}

	public uint GetVisibleInstanceCount(int lodLvl)
	{
		return stats[lodLvl].visibleInstanceCount;
	}

	public uint GetLODTotalVertexCount(int lodLvl)
	{
		return stats[lodLvl].totalVerts;
	}

	public uint GetLODTotalIndexCount(int lodLvl)
	{
		return stats[lodLvl].totalIndices;
	}
}
