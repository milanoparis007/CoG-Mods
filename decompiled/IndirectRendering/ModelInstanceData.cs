using UnityEngine;

namespace IndirectRendering;

public struct ModelInstanceData
{
	public Matrix4x4 trs;

	public Matrix4x4 paletteVars0;

	public Matrix4x4 paletteVars1;

	public Color overlayColor;

	public float paletteGroupIndex;

	public float springHueShift;

	public float fallHueShift;

	public float fogHideShift;

	public float snowThreshold;

	public int entityId;

	public int nodeId;

	public float pad3;
}
