using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

public class ModuleDescCapsuleContext : MonoBehaviour
{
	public string text;

	public ModuleDescCapsuleContext Set(string text)
	{
		this.text = text;
		return this;
	}
}
