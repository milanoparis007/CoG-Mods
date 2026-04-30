using System.Collections.Generic;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

internal class ModuleToggleContext : MonoBehaviour
{
	public ModuleSlot slotdef;

	public IModule module;

	public ModuleCommon common;

	public string mouseover;

	public bool HasMouseover => mouseover != null;

	public bool IsModuleInstalled => module != null;

	public void Reset()
	{
		slotdef = null;
		module = null;
		common = null;
		mouseover = null;
	}

	public void SetModule(int index, List<ModuleSlot> slotdefs, List<IModule> modules)
	{
		Reset();
		slotdef = slotdefs[index];
		module = modules[index];
		common = module?.ModuleConfig.Common;
	}

	public void SetOwner(VisitState visit)
	{
		Reset();
		string fullName = visit.npc.data.person.FullName;
		mouseover = Loc.Get("module.ui-util.mo.owner", "name", fullName);
	}

	public void SetCorner(VisitState visit)
	{
		Reset();
		string text = (visit.GetBldgNode() ?? visit.GetCrewNode())?.GetCornerNameShort() ?? string.Empty;
		mouseover = Loc.Get("module.ui-util.mo.corner", "corner", text).Trim();
	}

	public void SetBusiness(VisitState visit)
	{
		Reset();
		string text = visit.biz?.data.biz.bizname ?? string.Empty;
		mouseover = Loc.Get("module.ui-util.mo.biz", "bizname", text).Trim();
	}

	public void SetLocKey(string lockey)
	{
		Reset();
		mouseover = Loc.Get(lockey);
	}

	public string MakeModuleButtonText()
	{
		if (mouseover != null)
		{
			return mouseover;
		}
		if (module != null)
		{
			if (!module.IsEnabled(Game.ctx.clock.Now))
			{
				return ModulesUIUtil.DescribeEnable(module, Game.ctx.clock.Now, shortForm: true).text;
			}
			return ModulesUIUtil.FindModuleName(common);
		}
		return Loc.Get("module.ui-util.button.empty");
	}
}
