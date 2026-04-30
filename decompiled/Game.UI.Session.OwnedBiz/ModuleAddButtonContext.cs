using Game.Session.Data;
using Game.Session.Entities;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

internal class ModuleAddButtonContext : MonoBehaviour
{
	public ModuleSlot slotdef;

	public AddModuleDef addmoduledef;

	public ModQuery q;

	public bool canInstall;

	public string explainCosts;

	public void Set(ModuleSlot slotdef, AddModuleDef addmoduledef, ModQuery modQuery, bool canInstall, string explainCosts)
	{
		this.slotdef = slotdef;
		this.addmoduledef = addmoduledef;
		q = modQuery;
		this.canInstall = canInstall;
		this.explainCosts = explainCosts;
	}

	public void Reset()
	{
		slotdef = null;
		addmoduledef = default(AddModuleDef);
		q = default(ModQuery);
		canInstall = false;
		explainCosts = null;
	}
}
