using Game.Core;
using UnityEngine;

namespace Game.UI.Session.Popups;

public class TrophyCtx : MonoBehaviour
{
	public string signature;

	public Label id;

	public void Set(Label id, string signature)
	{
		this.signature = signature;
		this.id = id;
	}
}
