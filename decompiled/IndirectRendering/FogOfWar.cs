using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace IndirectRendering;

[Serializable]
[PostProcess(typeof(FogOfWarRenderer), PostProcessEvent.AfterStack, "Custom/Fog Of War", true)]
public sealed class FogOfWar : PostProcessEffectSettings
{
	[Header("General Effects")]
	[Tooltip("Fog of War will only be rendered during a play session, but this setting will force it to show always")]
	public BoolParameter ShowAlways = new BoolParameter
	{
		value = false
	};

	[Tooltip("Color for the game world to tint with for sepiatone effect")]
	public ColorParameter FogColor = new ColorParameter
	{
		value = Color.black
	};

	[Tooltip("Color for the scene to fade into at the edge of the map.")]
	public ColorParameter EdgeOfMapColor = new ColorParameter
	{
		value = Color.black
	};

	[Range(0f, 0.5f)]
	[Tooltip("Map percentage distance to being fading into fog")]
	public FloatParameter FogFadeDistance = new FloatParameter
	{
		value = 0.45f
	};

	[Header("Lighting")]
	[Tooltip("Color for the scene to fade into at the edge of the map.")]
	public ColorParameter FogOfWarLightColor = new ColorParameter
	{
		value = new Color(1f, 0.9686275f, 46f / 51f, 1f)
	};

	[Tooltip("Color for the scene to fade into at the edge of the map.")]
	public ColorParameter FogOfWarAmbientColor = new ColorParameter
	{
		value = new Color(0.7019608f, 0.7215686f, 0.8196079f, 1f)
	};
}
