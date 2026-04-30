using System.Collections.Generic;
using Game.Session;
using Game.Session.Data;

namespace Game.Services;

public sealed class UIQuery
{
	public enum Type
	{
		ConvoQuery,
		BuildingFn,
		CornerFn
	}

	public enum Test
	{
		None,
		QreqStart,
		QreqFinish,
		FrontDataUpdate,
		ProductionStalled,
		BuildingDamaged,
		HealPrompt,
		BizRecentTradeAi,
		BizRecentTradeHuman,
		DeliveryUpdate,
		ControlledUpdate,
		ReseventUpdate,
		CopPaidOff,
		CasinoOutOfCash,
		CanadaRelated,
		TerritoryUpdate,
		HeatUpdate
	}

	public Type type;

	public Test testfn;

	public VisitRequirementList visreqs;

	public List<SessionEventType> resetevents;

	public string icon;

	public string locdesc;

	public string GetIcon()
	{
		return Loc.Get(icon);
	}

	public string GetIconAndDesc()
	{
		return Loc.Get("ui.combine.icon-and-desc", "icon", Loc.Get(icon), "desc", Loc.Get(locdesc));
	}
}
