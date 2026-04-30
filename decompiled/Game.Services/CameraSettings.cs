using SomaSim.Util;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Game.Services;

[RequireComponent(typeof(Camera))]
[AddComponentMenu("Game UI/Camera Settings")]
public sealed class CameraSettings : MonoBehaviour
{
	public float minZZoom = 10f;

	public float maxZZoom = 95f;

	public float minXPitch = 35f;

	public float maxXPitch = 90f;

	public float startZZoom = 95f;

	public float startXPitch = 35f;

	public float startYRotation = 15f;

	public float mouseRotationMultiplier = 0.3f;

	public float mousePitchMultiplier = 0.2f;

	public float mouseZoomMultiplier = 0.3f;

	public float mouseZoomHeightMultiplier = 1f;

	public float minFarClip = 50f;

	public float maxFarClip = 200f;

	public float minFOV = 40f;

	public float maxFOV = 55f;

	public AnimationCurve farClipPlaneCurve;

	public AnimationCurve fovCurve;

	public float minShadowBias = 0.1f;

	public float maxShadowBias = 0.3f;

	public AnimationCurve shadowBiasCurve;

	public PostProcessVolume postFX;

	private void Start()
	{
		GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;
	}

	public float GetZoomPercentage(float zoom)
	{
		return (zoom - minZZoom) / (maxZZoom - minZZoom);
	}

	public float ClampZoom(float zoom)
	{
		return MathUtil.Clamp(zoom, minZZoom, maxZZoom);
	}

	public float ClampPitch(float pitch)
	{
		return MathUtil.Clamp(pitch, minXPitch, maxXPitch);
	}

	public float GetFarClipPlane(float zoomPercentage)
	{
		return Mathf.Lerp(minFarClip, maxFarClip, farClipPlaneCurve.Evaluate(zoomPercentage));
	}

	public float GetFOV(float zoomPercentage)
	{
		return Mathf.Lerp(minFOV, maxFOV, fovCurve.Evaluate(zoomPercentage));
	}

	public float GetShadowBias(float zoomPercentage)
	{
		return Mathf.Lerp(minShadowBias, maxShadowBias, shadowBiasCurve.Evaluate(zoomPercentage));
	}
}
