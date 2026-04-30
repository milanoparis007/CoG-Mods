using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class BizConfig : BaseConfig
{
	public ZoneType type;

	public int popneeded;

	public Label movesinto;

	public string icon;

	public string locname;

	public string locdesc;

	public string bizname;

	public string emptyImage;

	public ArrayList modulesInBuilding;

	public PlayerModulesConstraints playerModules;

	public ModValue playerTakeoverCost;

	public bool playerTakeoverDisabled;

	public VisitRequirementList reqsToInstall;

	public VisitRequirementList reqsToAttachToBuilding;

	public List<Label> skilltrack;

	public StarterPack starterPack;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.biz = new BizComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		BizData obj = source?.biz ?? new BizData();
		BizData result = obj;
		target.biz = obj;
		return result;
	}

	public string GetIcon()
	{
		return Loc.Get(icon);
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}

	public List<Label> PickModulesForBuilding(Entity e)
	{
		if (modulesInBuilding == null)
		{
			return new List<Label>(0);
		}
		Xorshift rng = e.data.ident.rng;
		List<Label> list = new List<Label>(modulesInBuilding.Count);
		foreach (object item in modulesInBuilding)
		{
			if (item is string)
			{
				list.Add(new Label(item as string));
			}
			else if (item is ArrayList)
			{
				ArrayList arrayList = item as ArrayList;
				int index = rng.Generate(0, arrayList.Count);
				string value = arrayList[index] as string;
				list.Add(new Label(value));
			}
		}
		return list;
	}

	public List<Label> GenerateSkillTrack(IRandom rng)
	{
		if (skilltrack == null || skilltrack.Count <= 0)
		{
			Logger.Warning("Empty skilltrack in ai business", icon);
			return new List<Label>();
		}
		Label id = rng.PickElement(skilltrack);
		return Game.serv.globals.settings.skills.GenerateSkillTrack(id, rng);
	}

	public override void VerifyAfterLoading(EntityConfig config)
	{
		if (!config.ident.IsChild)
		{
			return;
		}
		if (modulesInBuilding != null)
		{
			foreach (object item in modulesInBuilding)
			{
				if (item is string || !(item is ArrayList arrayList))
				{
					continue;
				}
				foreach (object item2 in arrayList)
				{
					_ = item2 is string;
				}
			}
		}
		if (playerModules != null)
		{
			_ = Game.serv.globals.settings.tags.allTags;
		}
		if (skilltrack == null)
		{
			return;
		}
		_ = Game.serv.globals.settings.skills.aiskilltracks;
		foreach (Label item3 in skilltrack)
		{
			_ = item3;
		}
	}

	private bool IsValidZone(ZoneType zone)
	{
		if (zone != ZoneType.Com && zone != ZoneType.Ind)
		{
			return zone == ZoneType.Res;
		}
		return true;
	}
}
