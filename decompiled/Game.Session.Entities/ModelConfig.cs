using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Heatmaps;

namespace Game.Session.Entities;

public sealed class ModelConfig : BaseConfig
{
	public enum LoadType
	{
		Immediate = 0,
		Vehicle = 1,
		Building = 2,
		Deco = 5,
		Other = 10
	}

	public enum SlotOverride
	{
		None,
		RowShort,
		RowTall,
		BoardwalkBookendStart,
		BoardwalkBookendEnd
	}

	public class AutoPlacementSettings
	{
		public AppealLevel appeal = AppealLevel.Any;

		public GroundFlags ground = GroundFlags.Any;

		public ZoneTypeFlags zone = ZoneTypeFlags.Any;

		public float weight = 1f;

		internal bool DoesMatch(AppealLevel mapAppeal, ZoneTypeFlags mapZoneFlags, GroundFlags mapGroundFlags)
		{
			if (MatchBits(15, (int)appeal, (int)mapAppeal) && MatchBits(15, (int)zone, (int)mapZoneFlags))
			{
				return MatchBits(31, (int)ground, (int)mapGroundFlags);
			}
			return false;
		}
	}

	public LoadType loadtype = LoadType.Other;

	public bool suppressload;

	public List<string> prefabs;

	public List<string> unknowns;

	public ModelSlotContainer slots;

	public ModelSlotContainer unknownslots;

	public Specials slotOverrides;

	public WorldSize customIndirectOffset;

	public int paletterow;

	public bool seasonal;

	public bool hidesInFog;

	public int paletterowunknown;

	public bool indirectInstancing;

	public bool hillsokay;

	public AutoPlacementSettings autoplace;

	public override List<Type> RequiresConfigs => null;

	public bool ShouldLoadImmediately => loadtype == LoadType.Immediate;

	public int LoadingPriority => (int)loadtype;

	public float FogHideShift
	{
		get
		{
			if (!hidesInFog)
			{
				return 0f;
			}
			return 1f;
		}
	}

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.model = new ModelComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		ModelData obj = source?.model ?? new ModelData();
		ModelData result = obj;
		target.model = obj;
		return result;
	}

	private static bool MatchBits(int dontCareFlags, int myRequirements, int incomingFlags)
	{
		if (myRequirements == dontCareFlags)
		{
			return true;
		}
		if (myRequirements == 0)
		{
			return incomingFlags == 0;
		}
		return (myRequirements & incomingFlags) != 0;
	}

	public ModelSlotContainer GetSlotOverrideOrNull(SlotOverride type)
	{
		return type switch
		{
			SlotOverride.RowShort => slotOverrides?.matchingRowShort, 
			SlotOverride.RowTall => slotOverrides?.matchingRowTall, 
			SlotOverride.BoardwalkBookendEnd => slotOverrides?.boardwalkBookendEnd, 
			SlotOverride.BoardwalkBookendStart => slotOverrides?.boardwalkBookendStart, 
			SlotOverride.None => null, 
			_ => null, 
		};
	}
}
