using Game.Core;
using UnityEngine;

namespace Game.Session.Assets;

public sealed class ModelProxyContainerComponent : MonoBehaviour
{
	public int DebugIndex;

	public EntityID eid = 0uL;

	public void Set(EntityID eid)
	{
		this.eid = eid;
		DebugIndex = eid.index;
	}

	public void Reset()
	{
		Set(0uL);
	}

	public static EntityID FindEntityId(GameObject go)
	{
		ModelProxyContainerComponent component = go.GetComponent<ModelProxyContainerComponent>();
		if (!(component != null))
		{
			return EntityID.INVALID;
		}
		return component.eid;
	}
}
