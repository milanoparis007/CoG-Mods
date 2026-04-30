using Game.Core;

namespace Game.Session.Player;

public struct OffBoardInfo
{
	public EntityID crew;

	public Label vehTemplate;

	public PlayerCrewData.OffBoardReason reason;

	public OffBoardInfo(EntityID crew, Label vehTemplate, PlayerCrewData.OffBoardReason reason)
	{
		this.crew = crew;
		this.vehTemplate = vehTemplate;
		this.reason = reason;
	}
}
