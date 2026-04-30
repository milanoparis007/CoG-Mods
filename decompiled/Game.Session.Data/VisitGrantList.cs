using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Crew;
using SomaSim.Util;

namespace Game.Session.Data;

public class VisitGrantList : List<VisitGrant>
{
	private static readonly GrantReq[] ALL_REQUIREMENTS = Enum.GetValues(typeof(GrantReq)) as GrantReq[];

	private static string CANADA_BUILDING = "canadahouse";

	public void ApplyAll(GrantContext ctx)
	{
		VisitGrant.SelectorRequest selectorRequest = VisitGrant.SelectorRequest.None;
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				VisitGrant.SelectorRequest selectorRequest2 = enumerator.Current.RequestSelector();
				if (selectorRequest2 != VisitGrant.SelectorRequest.None && selectorRequest != VisitGrant.SelectorRequest.None && selectorRequest2 != selectorRequest)
				{
					Logger.Warning($"Grants for quest: {ctx.quuid} requesting multiple types of selectors. Currently unimplemented!");
				}
				if (selectorRequest2 != VisitGrant.SelectorRequest.None)
				{
					selectorRequest = selectorRequest2;
				}
			}
		}
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				VisitGrant.SelectorRequest selectorRequest3 = enumerator.Current.RequestSelector();
				if (selectorRequest3 != VisitGrant.SelectorRequest.None)
				{
					PopSpecificEIDSelector(selectorRequest3, ctx);
					return;
				}
			}
		}
		DoApplyAll(ctx);
	}

	private void DoApplyAll(GrantContext ctx)
	{
		GrantReq fulfilled = ctx.FindFulfilledReqs();
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			VisitGrant current = enumerator.Current;
			GrantReq grantReq = FindFailedRequirements(current, fulfilled);
			if (grantReq == GrantReq.Nothing)
			{
				current.Apply(ctx);
			}
			else
			{
				Logger.Warning($"Grant {current} did not receive required context data: {grantReq}");
			}
		}
	}

	private GrantReq FindFailedRequirements(VisitGrant grant, GrantReq fulfilled)
	{
		GrantReq requiredContext = grant.RequiredContext;
		if (requiredContext == GrantReq.Nothing)
		{
			return GrantReq.Nothing;
		}
		GrantReq grantReq = GrantReq.Nothing;
		int i = 0;
		for (int num = ALL_REQUIREMENTS.Length; i < num; i++)
		{
			GrantReq grantReq2 = ALL_REQUIREMENTS[i];
			if (grantReq2 != GrantReq.Nothing)
			{
				bool num2 = (requiredContext & grantReq2) == grantReq2;
				bool flag = (fulfilled & grantReq2) == grantReq2;
				if (num2 && !flag)
				{
					grantReq |= grantReq2;
				}
			}
		}
		return grantReq;
	}

	public string Describe(GrantContext ctx, bool multiline)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				string text = enumerator.Current.Describe(ctx);
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				if (multiline)
				{
					stringBuilder.AppendLine(Loc.Get("convo.delivery-quest-grant", "item", text));
					continue;
				}
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(", ");
				}
				stringBuilder.Append(text);
			}
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	private void PopSpecificEIDSelector(VisitGrant.SelectorRequest requestType, GrantContext ctx)
	{
		switch (requestType)
		{
		case VisitGrant.SelectorRequest.Building:
			EntitySelectionPopup.ShowBuildingSelectorWithCapacity((from x in Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe()
				where !x.FindEntity().components.building.HasTag(new Label(CANADA_BUILDING)) && x.FindEntity().components.modules.gambling == null
				select x).ToList(), Loc.Get("ui.grants.target-selector.building"), delegate(EntityID eid)
			{
				ctx.buildingTarget = eid;
				DoApplyAll(ctx);
			}, delegate
			{
				DoApplyAll(ctx);
			});
			break;
		case VisitGrant.SelectorRequest.Crew:
			EntitySelectionPopup.ShowCrewSelector((from x in Game.ctx.players.Human.crew.GetLiving()
				select x.peepId).ToList(), Loc.Get("ui.grants.target-selector.crew"), delegate(EntityID eid)
			{
				ctx.crewTarget = eid;
				DoApplyAll(ctx);
			}, delegate
			{
				DoApplyAll(ctx);
			});
			break;
		case VisitGrant.SelectorRequest.Vehicle:
			EntitySelectionPopup.ShowVehicleSelector(Game.ctx.players.Human.crew.AllVehicles.ToList(), Loc.Get("ui.grants.target-selector.vehicle"), delegate(EntityID eid)
			{
				ctx.vehicleTarget = eid;
				DoApplyAll(ctx);
			}, delegate
			{
				DoApplyAll(ctx);
			});
			break;
		}
	}
}
