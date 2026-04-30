using Game.Session.Player.Commands;
using Game.UI.Mouseovers;

namespace Game.UI.Session.Crew;

internal class CrewCardCommandMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTR;

	protected override string ProduceText()
	{
		return CommandButtonUtil.FindButtonStateOrNull(context)?.status.mouseover;
	}
}
