using System.Text;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

internal sealed class ViewFrontPage : SubviewEntry
{
	private const string HEADER = "Header";

	private const string TEXT = "Text";

	public ViewFrontPage(ViewType type, GameObject go)
		: base(type, go)
	{
	}

	public override void RefreshSubview()
	{
		string text = Loc.Get("ui.viewdescribemodule.opdetails.header", "info", Loc.Get("ui.viewdescribemodule.frontpage.header"));
		go.SetText("Header", text);
		go.SetText("Text", MakeDescription());
	}

	private string MakeDescription()
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		PersonData person = Model.visit.npc.data.person;
		ModulesComponent modules = Model.visit.building.components.modules;
		bool flag = false;
		stringBuilder.AppendLine(Loc.Get("ui.viewdescribemodule.frontpage.text", "owner", person.FullName));
		if (modules.FindFrontroomModule() != null)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(Loc.Get("ui.viewdescribemodule.frontpage.frontbiz", "owner", person.FullName, "name", Model.visit.biz.data.biz.bizname));
		}
		else
		{
			flag = true;
		}
		IModule module = modules.FindBackroomModule();
		if (module != null)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(Loc.Get("ui.viewdescribemodule.frontpage.backbiz", "name", Loc.Get(module.ModuleConfig.Common.display.locname)));
		}
		else
		{
			flag = true;
		}
		if (flag)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(Loc.Get("ui.viewdescribemodule.frontpage.empty"));
		}
		if (modules.inventory != null)
		{
			ModuleCommon.Display display = modules.inventory.ModuleConfig.Common.display;
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(Loc.Get("ui.viewdescribemodule.frontpage.inventory", "name", Loc.Get(display.locname), "desc", Loc.Get(display.locdesc)));
		}
		stringBuilder.AppendLine();
		stringBuilder.AppendLine(Loc.Get("ui.viewdescribemodule.frontpage.footer"));
		return stringBuilder.ToStringAndReturnToPool();
	}
}
