using System;
using System.Diagnostics;
using SomaSim.Util;

namespace Game.Services.Mods;

[DebuggerDisplay("{DebugString}")]
public class ModMetadata
{
	private const int MAX_DESC_LENGTH = 500;

	public ModID modid;

	public string name;

	public string description;

	public string workshopid;

	public bool IsLocalOnly
	{
		get
		{
			if (modid.source == ModID.Source.Local)
			{
				return workshopid == null;
			}
			return false;
		}
	}

	public bool IsLocalShared
	{
		get
		{
			if (modid.source == ModID.Source.Local)
			{
				return workshopid != null;
			}
			return false;
		}
	}

	public bool IsWorkshop => modid.source == ModID.Source.Workshop;

	public bool CanBeShared => modid.source == ModID.Source.Local;

	public bool CanBeDeleted => modid.source == ModID.Source.Local;

	private string DebugString => ToString();

	public string GetIconAndName()
	{
		return Loc.Get("ui.export.modiconandname", "name", name);
	}

	internal object GetDesc()
	{
		return description?.TrimToLength(500, addEllipsis: true);
	}

	public override string ToString()
	{
		return $"[ModDef {modid} / {name} / {workshopid}]";
	}

	public ulong GetWorkshopId()
	{
		try
		{
			return string.IsNullOrEmpty(workshopid) ? 0 : Convert.ToUInt64(workshopid);
		}
		catch (Exception ex)
		{
			Logger.Warning("Failed to process workshop id: " + workshopid + "\n" + ex.Message);
			return 0uL;
		}
	}
}
