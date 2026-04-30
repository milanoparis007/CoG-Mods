using Game.Services;
using UnityEngine;

namespace Game.UI.Mouseovers;

[AddComponentMenu("Game UI/Text Mouseover Ctx")]
public class TextMouseoverContext : MonoBehaviour
{
	public string lockey;

	public string GetText()
	{
		return Loc.Get(lockey);
	}
}
