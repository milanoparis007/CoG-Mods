using System.Collections.Generic;

namespace Game.UI.Session.HUD;

public class HUDDialogButtonSetDef : HUDDialogItemDefBase
{
	public List<HUDDialogButtonDef> buttons = new List<HUDDialogButtonDef>();

	protected HUDDialogButtonSetDef()
	{
	}
}
