using Game.Core;

namespace Game.Session.Setup;

public class TransitTileset
{
	public Label IIntersectionTmpl;

	public Label LIntersectionTmpl;

	public Label TIntersectionTmpl;

	public Label FourWayIntersectionTmpl;

	public Label EndNodeTmpl;

	public Label VerticalEdgeTmpl;

	public Label VerticalEdgeStartTmpl;

	public Label VerticalBetweenGridsTmpl;

	public Label BridgeStartTmpl;

	public Label BridgeSlopeTmpl;

	public Label BridgeSmallTrussTmpl;

	public Label BridgeTrussTmpl;

	public RoadBeadPlacement BridgeStartBead;

	public RoadBeadPlacement BridgeSlopeBead;

	public RoadBeadPlacement BridgeSmallTrussBead;

	public RoadBeadPlacement BridgeTrussBead;

	public int IntersectionSize;

	public int IntersectionHalfSize => IntersectionSize / 2;
}
