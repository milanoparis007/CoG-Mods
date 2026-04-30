using Game.Session.Entities;
using IndirectRendering;
using UnityEngine;

namespace Game.Session.Assets;

public sealed class ModelSubProxy
{
	public string prefabName;

	public GameObject model;

	public DynamicMeshHandle dynamicHandle;

	public IndirectHandle indirectHandle;

	public ModelSlot slotData;
}
