using Game.Services;
using UnityEngine;

namespace Game.UI.Session.OwnedGambling;

public class AmenityAddButtonCtx : MonoBehaviour
{
	public bool canInstall;

	public AmenityDef def;

	public void Set(AmenityDef def, bool canInstall)
	{
		this.canInstall = canInstall;
		this.def = def;
	}
}
