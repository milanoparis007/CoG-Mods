using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI.Session.OwnedBiz;

public class ModuleDescManagerMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		ViewDescribeModule.ManagerButtonContext component = context.GetComponent<ViewDescribeModule.ManagerButtonContext>();
		if (component.manager.IsNotValid)
		{
			if (component.pid.FindPlayer().crew.LivingCrewCount <= 1)
			{
				return Loc.Get("module.mgmt.unavailable");
			}
			return Loc.Get("module.mgmt.available");
		}
		Entity entity = component.manager.FindEntity();
		(string name, string relstr) npcNameAndRelationshipToPlayer = NameUtils.GetNpcNameAndRelationshipToPlayer(entity, PlayerID.HumanPlayer);
		string item = npcNameAndRelationshipToPlayer.name;
		string item2 = npcNameAndRelationshipToPlayer.relstr;
		string text = entity.components.person.GetAllTraits().SelectToString((Trait trait) => trait.GetLocName(), ", ");
		string text2 = PersonInfoUtil.GenerateLevelupDescription(entity, detailed: false) ?? "";
		return Loc.Get("module.mgmt.desc", "name", item, "relstr", item2, "traits", text, "levelups", text2);
	}
}
