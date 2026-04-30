using Game;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace IndirectRendering;

public sealed class FogOfWarRenderer : PostProcessEffectRenderer<FogOfWar>
{
	public override void Render(PostProcessRenderContext context)
	{
		if (base.settings.ShowAlways.value || global::Game.Game.ctx != null)
		{
			float num = global::Game.Game.ctx?.seasons?.GetHourSunColorMultiplier().GetLuminosity() ?? 1f;
			PropertySheet propertySheet = context.propertySheets.Get(Shader.Find("Hidden/FogOfWar"));
			propertySheet.properties.SetColor("_EdgeOfMapColor", base.settings.EdgeOfMapColor.value * num);
			propertySheet.properties.SetFloat("_FogFadeDistance", base.settings.FogFadeDistance);
			Shader.SetGlobalColor("_FogOfWarColor", base.settings.FogColor.value * num);
			Shader.SetGlobalColor("_FogOfWarLightColor", base.settings.FogOfWarLightColor);
			Shader.SetGlobalColor("_FogOfWarAmbientColor", base.settings.FogOfWarAmbientColor);
			Camera camera = context.camera;
			Matrix4x4 gPUProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, renderIntoTexture: false);
			float value = (gPUProjectionMatrix[3, 2] = 0f);
			gPUProjectionMatrix[2, 3] = value;
			gPUProjectionMatrix[3, 3] = 1f;
			Matrix4x4 value2 = Matrix4x4.Inverse(gPUProjectionMatrix * camera.worldToCameraMatrix) * Matrix4x4.TRS(new Vector3(0f, 0f, 0f - gPUProjectionMatrix[2, 2]), Quaternion.identity, Vector3.one);
			propertySheet.properties.SetMatrix("clipToWorld", value2);
			context.command.BlitFullscreenTriangle(context.source, context.destination, propertySheet, 0);
		}
	}
}
