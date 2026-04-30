using UnityEngine;

namespace Game.UI.Session.Ledger;

public abstract class LedgerDialogSubview : Subview<LedgerModel, LedgerDialog, LedgerController>
{
	protected LedgerDialogSubview(GameObject go, string panel, LedgerController c)
		: base(go, panel, c)
	{
	}
}
