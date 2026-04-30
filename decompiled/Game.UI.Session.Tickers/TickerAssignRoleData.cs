using Game.Services;
using Game.Session.Data;

namespace Game.UI.Session.Tickers;

public class TickerAssignRoleData : TickerData
{
	public RoleDef role;

	public override bool OnClickCallback()
	{
		Game.serv.ui.AddPopup(new RoleAssignPopup(role));
		return true;
	}
}
