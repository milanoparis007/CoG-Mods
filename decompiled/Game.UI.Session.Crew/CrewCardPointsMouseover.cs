using Game.UI.Mouseovers;

namespace Game.UI.Session.Crew;

internal class CrewCardPointsMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTR;

	protected override string ProduceText()
	{
		CrewCardContext componentInParent = context.GetComponentInParent<CrewCardContext>();
		if (!(componentInParent != null))
		{
			return null;
		}
		return componentInParent.GeneratePointsMouseover(details: true);
	}
}
