using System.Collections.Generic;
using System.Text;
using Game.Services;
using Game.Session.Player.KB;
using SomaSim.Util;

namespace Game.UI.Session.Picks;

public static class BasePickUtil
{
	public static string GeneratePipDescriptions(List<KBResult> pips)
	{
		if (pips == null || pips.Count == 0)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		foreach (KBResult pip in pips)
		{
			if (!pip.showpip)
			{
				continue;
			}
			UIQuery uIQuery = pip.FindQuery();
			if (uIQuery.icon != null && uIQuery.locdesc != null)
			{
				if (stringBuilder.Length != 0)
				{
					stringBuilder.AppendLine();
				}
				stringBuilder.Append(uIQuery.GetIconAndDesc());
			}
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	public static string GeneratePipIcons(bool showpips, List<KBResult> pips)
	{
		if (!showpips || pips == null || pips.Count == 0)
		{
			return string.Empty;
		}
		using ListPool<string>.PooledBlockList pooledBlockList = ListPool<string>.Allocate();
		foreach (KBResult pip in pips)
		{
			if (pip.IsValid && pip.showpip)
			{
				UIQuery uIQuery = pip.FindQuery();
				if (uIQuery.icon != null)
				{
					pooledBlockList.Add(uIQuery.GetIcon());
				}
			}
		}
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		for (int i = 0; i < pooledBlockList.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(" ");
			}
			if (i == 2 && pooledBlockList.Count > 3)
			{
				stringBuilder.Append("…");
				break;
			}
			stringBuilder.Append(pooledBlockList[i]);
		}
		return stringBuilder.ToStringAndReturnToPool();
	}
}
