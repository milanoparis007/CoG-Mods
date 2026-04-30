using Game.UI.Mouseovers;

namespace Game.UI.Session.Crew;

internal class CrewCardContentsMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTR;

	protected override string ProduceText()
	{
		CrewCardContext componentInParent = context.GetComponentInParent<CrewCardContext>();
		if (!(componentInParent != null))
		{
			return null;
		}
		return componentInParent.data.gen.GetExtraInventoryMO();
	}
}
