using Game.Session.Assets;
using UnityEngine;

namespace IndirectRendering;

public class PerInstanceRuntimeData
{
	public Vector3 position;

	public float rotation;

	public Vector3 offset;

	public VertexPaletteData _paletteData;

	public float springHueShift;

	public float fallHueShift;

	public float fogHideShift;

	public float snowThreshold;

	public Color overlayColor;

	public int entityId = -1;

	public int nodeId = -1;
}
