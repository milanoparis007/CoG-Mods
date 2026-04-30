using Game.Services;
using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI.Session.OwnedGambling;

public class AmenityStatusMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		AmenityMouseoverCtx componentInObjectOrParents = context.GetComponentInObjectOrParents<AmenityMouseoverCtx>();
		string text = "";
		if (!componentInObjectOrParents.funded)
		{
			text = text + Loc.Get("ui.ownedcasino.amenity.needs-cash.mo") + "\n";
		}
		if (!componentInObjectOrParents.hasManager)
		{
			text = text + Loc.Get("ui.ownedcasino.amenity.needs-manager.mo") + "\n";
		}
		if (!componentInObjectOrParents.notDamaged)
		{
			text = text + Loc.Get("ui.ownedcasino.amenity.needs-repair.mo") + "\n";
		}
		if (!(componentInObjectOrParents.funded & componentInObjectOrParents.hasManager & componentInObjectOrParents.notDamaged))
		{
			return text;
		}
		return Loc.Get("ui.ownedcasino.amenity.funded.mo");
	}
}
