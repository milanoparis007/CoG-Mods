using UnityEngine;

namespace Game.UI.Session;

internal sealed class CardContext : MonoBehaviour
{
	public PersonInfoModel model;

	public CardContextData data;

	public void Set(PersonInfoModel model, CardContextData data)
	{
		this.model = model;
		this.data = data;
	}

	public void Reset()
	{
		Set(null, null);
	}
}
