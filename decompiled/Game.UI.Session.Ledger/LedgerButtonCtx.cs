using Game.Services;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Session.Ledger;

internal class LedgerButtonCtx : MonoBehaviour
{
	public LedgerButtonDef def;

	public void Initialize(LedgerButtonDef def, ToggleGroup group, UnityAction<LedgerButtonDef> callback)
	{
		this.def = def;
		base.gameObject.SetText("Toggle/Text", Loc.Get(def.icon));
		Toggle toggle = base.gameObject.GetToggle("Toggle");
		toggle.group = group;
		toggle.onValueChanged.SetListener(delegate(bool isOn)
		{
			if (isOn)
			{
				callback(def);
			}
		});
	}
}
