using Game.Services;
using UnityEngine;

namespace Game.UI;

public class DecisionCtx : MonoBehaviour
{
	public Decision info;

	public void Set(Decision dec)
	{
		info = dec;
	}
}
