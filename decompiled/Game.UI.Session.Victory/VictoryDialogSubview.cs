using UnityEngine;

namespace Game.UI.Session.Victory;

public abstract class VictoryDialogSubview : Subview<VictoryModel, VictoryDialog, VictoryController>
{
	protected VictoryDialogSubview(GameObject go, string panel, VictoryController c)
		: base(go, panel, c)
	{
	}
}
