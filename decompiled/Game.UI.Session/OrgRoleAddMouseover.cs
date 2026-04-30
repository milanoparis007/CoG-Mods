using System.Linq;
using Game.Services;
using Game.Session.Data;
using Game.UI.Mouseovers;
using UnityEngine.UI;

namespace Game.UI.Session;

public class OrgRoleAddMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		string text = "";
		OrgRoleAddMouseoverCtx component = context.GetComponent<OrgRoleAddMouseoverCtx>();
		RoleDef roleById = Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(component.roleId);
		if (context.GetComponent<Button>() != null)
		{
			text = text + Loc.Get("ui.orgchart.role-reqs.header") + "\n\n";
			text = text + Loc.Get("ui.orgchart.role-reqs.item.formatting", "item", Loc.Get("ui.orgchart.role-reqs.captain")) + "\n";
			foreach (IVisitRequirement req in roleById.reqs)
			{
				if (req is CheckCrewStat checkCrewStat)
				{
					text = text + Loc.Get("ui.orgchart.role-reqs.item.formatting", "item", Loc.GetCrewStatWithValueSpace(checkCrewStat.id, checkCrewStat.value.ToString())) + "\n";
				}
			}
			int num = Game.ctx.players.Human.crew.GetQualifiedForRole(component.roleId).Count();
			if (num >= 0)
			{
				text = text + "\n" + Loc.GetPluralized("ui.orgchart.role-reqs.eligible", num, "count", num);
			}
		}
		else
		{
			text = text + Loc.Get("ui.orgchart.role-reqs.title", "name", Loc.Get(roleById.loctitle)) + "\n\n";
			text = text + Loc.Get(roleById.locdesc) + "\n\n";
			text += Loc.Get("ui.crewinfo.descaction", "action", Loc.Get(roleById.locshort));
		}
		return text;
	}
}
