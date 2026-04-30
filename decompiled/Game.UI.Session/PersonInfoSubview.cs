using Game.Services;
using UnityEngine;

namespace Game.UI.Session;

public abstract class PersonInfoSubview : Subview<PersonInfoModel, PersonInfoDialog, PersonInfoController>
{
	public readonly PanelType panelType;

	public readonly string locicon;

	public PersonInfoSubview(GameObject go, string panelName, PersonInfoController controller, PanelType type, string locicon)
		: base(go, panelName, controller)
	{
		this.locicon = locicon;
		panelType = type;
	}

	public string GetIcon()
	{
		return Loc.Get(locicon);
	}
}
