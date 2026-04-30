using Game.Core;
using Game.Session.Board;
using Game.Session.Data;

namespace Game.UI.Session.Tickers;

public static class TickerDataTargetExensions
{
	public static WorldPos? GetWorldPosOrNull(this TickerTarget target)
	{
		if (!target.nodeId.IsValid)
		{
			if (!target.entityId.IsValid)
			{
				return null;
			}
			return BoardUtil.FindBoardPositionFor(target.entityId);
		}
		return target.nodeId.FindNode().pos;
	}

	public static void TweenCamera(this TickerTarget target)
	{
		PersonInfoUtil.TweenCameraToPos(target.GetWorldPosOrNull(), showFx: true);
	}
}
