using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class LotConfig : BaseConfig
{
	public struct Replacement
	{
		public ZoneType zone;

		public List<Label> templates;

		public float appeal;
	}

	public List<Replacement> lotdecos;

	public List<Replacement> upgrades;

	public List<Replacement> boardwalk;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.lot = new LotComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		LotData obj = source?.lot ?? new LotData();
		LotData result = obj;
		target.lot = obj;
		return result;
	}

	public Label FindUpgrade(Entity lot, ZoneType zone, IRandom rng, float appeal)
	{
		return FindReplacement(lot, upgrades, zone, rng, appeal);
	}

	public Label FindLotDeco(Entity lot, ZoneType zone, IRandom rng, float appeal)
	{
		return FindReplacement(lot, lotdecos, zone, rng, appeal);
	}

	public Label FindBoardwalk(Entity lot, ZoneType zone, IRandom rng, float appeal)
	{
		return FindReplacement(lot, boardwalk, zone, rng, appeal);
	}

	public static Label FindReplacement(Entity lot, List<Replacement> replacements, ZoneType zone, IRandom rng, float appeal)
	{
		if (replacements == null)
		{
			return Label.NULL;
		}
		VisitState visit = VisitState.MakeForBuilding(lot);
		for (int i = 0; i < replacements.Count; i++)
		{
			Replacement replacement = replacements[i];
			if (zone != replacement.zone || appeal < replacement.appeal)
			{
				continue;
			}
			if (replacement.templates == null)
			{
				Logger.Warning("Missing replacement templates in zone " + zone);
				continue;
			}
			using ListPool<EntityConfig>.PooledBlockList pooledBlockList = ListPool<EntityConfig>.Allocate();
			TestAndPopulate(replacement.templates, pooledBlockList, visit);
			return rng.PickElement(pooledBlockList).Template;
		}
		return Label.NULL;
	}

	private static void TestAndPopulate(List<Label> candidates, List<EntityConfig> results, VisitState visit)
	{
		EntityManager entityman = Game.ctx.entityman;
		foreach (Label candidate in candidates)
		{
			EntityConfig entityConfig = entityman.FindTemplate(candidate);
			if (entityConfig != null)
			{
				VisitRequirementList visitRequirementList = entityConfig.board?.reqsToUseAsUpgrade;
				if (visitRequirementList == null || visitRequirementList.AllPass(visit))
				{
					results.Add(entityConfig);
				}
			}
		}
	}
}
