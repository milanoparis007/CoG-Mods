using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class RespectBoostConfig
{
	public Label id;

	public Label socialAction;

	public Price cashCost;

	public CrewCost crewCost;

	public VisitGrantList grants;
}
