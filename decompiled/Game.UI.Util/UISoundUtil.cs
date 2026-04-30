using Game.Services.Audio;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Util;

public static class UISoundUtil
{
	public static void AttachSFXToAllClickables(GameObject go)
	{
		go.WalkChildren(AttachSFXToGameObject);
	}

	public static void AttachSFXToGameObject(Transform tr)
	{
		AttachSFXToGameObject(tr.gameObject);
	}

	public static void AttachSFXToGameObject(GameObject go)
	{
		if (go.GetComponent<Button>() != null || go.GetComponent<Toggle>() != null || go.GetComponent<Dropdown>() != null)
		{
			go.GetOrAddComponent<PlayUISound>();
		}
	}
}
