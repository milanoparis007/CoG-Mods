using System;
using System.Reflection;
using Game.Core;
using Game.Services;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class CameraMapBoundsPatch
	{
		private const float CameraMapBoundsPadding = 1f;
		private const float MinimumEdgeViewAllowance = 24f;
		private const float EdgeViewAllowanceViewportFraction = 0.35f;
		private const float MaximumEdgeViewAllowanceMapFraction = 0.35f;
		private const float CenterOverscrollViewportFraction = 0.12f;
		private const float MinimumCenterOverscroll = 12f;
		private const float MaximumCenterOverscrollMapFraction = 0.2f;
		private const float ClampLogIntervalSeconds = 10f;

		private static float _lastClampLogTime = -999f;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo setPosition = typeof(CameraService).GetMethod(
					"SetPosition",
					BindingFlags.Instance | BindingFlags.Public,
					null,
					new[] { typeof(CameraPos), typeof(CameraTween?) },
					null);
				if (setPosition == null)
				{
					Debug.LogWarning("[GameplayTweaks] CameraMapBoundsPatch: CameraService.SetPosition(CameraPos, CameraTween?) not found");
					return;
				}

				harmony.Patch(setPosition, prefix: new HarmonyMethod(typeof(CameraMapBoundsPatch), nameof(SetPositionPrefix)));
				VerificationLog("CameraBounds", "camera map-bounds clamp patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CameraMapBoundsPatch failed: " + ex.Message);
			}
		}

		private static void SetPositionPrefix(ref CameraPos pos)
		{
			try
			{
				if (!TryGetMapSize(out IntSize mapSize))
				{
					return;
				}

				CameraPos currentPos = global::Game.Game.serv.camera.GetPosition();
				WorldViewportOffsets offsets = CalculateCurrentViewportOffsets(currentPos);
				float clampedX = ClampAxis(pos.x, offsets.MinX, offsets.MaxX, mapSize.width, out float allowanceX);
				float clampedZ = ClampAxis(pos.z, offsets.MinZ, offsets.MaxZ, mapSize.height, out float allowanceZ);
				if (Math.Abs(clampedX - pos.x) < 0.001f && Math.Abs(clampedZ - pos.z) < 0.001f)
				{
					return;
				}

				float oldX = pos.x;
				float oldZ = pos.z;
				pos = new CameraPos(clampedX, clampedZ);
				MaybeLogClamp(oldX, oldZ, clampedX, clampedZ, mapSize, allowanceX, allowanceZ);
			}
			catch
			{
			}
		}

		private struct WorldViewportOffsets
		{
			public float MinX;
			public float MaxX;
			public float MinZ;
			public float MaxZ;
		}

		private static WorldViewportOffsets CalculateCurrentViewportOffsets(CameraPos currentPos)
		{
			WorldViewportOffsets offsets = new WorldViewportOffsets
			{
				MinX = 0f,
				MaxX = 0f,
				MinZ = 0f,
				MaxZ = 0f
			};

			AddScreenOffset(ref offsets, currentPos, new Vector2(0f, 0f));
			AddScreenOffset(ref offsets, currentPos, new Vector2(Screen.width, 0f));
			AddScreenOffset(ref offsets, currentPos, new Vector2(0f, Screen.height));
			AddScreenOffset(ref offsets, currentPos, new Vector2(Screen.width, Screen.height));
			return offsets;
		}

		private static void AddScreenOffset(ref WorldViewportOffsets offsets, CameraPos currentPos, Vector2 screenPos)
		{
			WorldPos worldPos = global::Game.Game.serv.camera.ScreenToWorldPos(screenPos);
			float offsetX = worldPos.x - currentPos.x;
			float offsetZ = worldPos.y - currentPos.z;
			offsets.MinX = Mathf.Min(offsets.MinX, offsetX);
			offsets.MaxX = Mathf.Max(offsets.MaxX, offsetX);
			offsets.MinZ = Mathf.Min(offsets.MinZ, offsetZ);
			offsets.MaxZ = Mathf.Max(offsets.MaxZ, offsetZ);
		}

		private static float ClampAxis(float requested, float viewportMinOffset, float viewportMaxOffset, float mapExtent, out float edgeAllowance)
		{
			float viewportSpan = Math.Max(0f, viewportMaxOffset - viewportMinOffset);
			edgeAllowance = Mathf.Clamp(
				viewportSpan * EdgeViewAllowanceViewportFraction,
				Math.Min(MinimumEdgeViewAllowance, mapExtent * 0.25f),
				mapExtent * MaximumEdgeViewAllowanceMapFraction);
			float min = CameraMapBoundsPadding - viewportMinOffset - edgeAllowance;
			float max = mapExtent - CameraMapBoundsPadding - viewportMaxOffset + edgeAllowance;
			if (min > max)
			{
				float centerOverscroll = Mathf.Clamp(
					viewportSpan * CenterOverscrollViewportFraction,
					Math.Min(MinimumCenterOverscroll, mapExtent * 0.1f),
					mapExtent * MaximumCenterOverscrollMapFraction);
				min = -centerOverscroll;
				max = mapExtent + centerOverscroll;
			}

			float hardCenterAllowance = Mathf.Clamp(
				viewportSpan * CenterOverscrollViewportFraction,
				Math.Min(MinimumCenterOverscroll, mapExtent * 0.1f),
				mapExtent * MaximumCenterOverscrollMapFraction);
			return Mathf.Clamp(
				Mathf.Clamp(requested, -hardCenterAllowance, mapExtent + hardCenterAllowance),
				min,
				max);
		}

		private static bool TryGetMapSize(out IntSize mapSize)
		{
			mapSize = default(IntSize);
			try
			{
				mapSize = global::Game.Game.ctx?.session?.mapconfig?.map?.mapSize ?? default(IntSize);
				return mapSize.width > 0 && mapSize.height > 0;
			}
			catch
			{
				return false;
			}
		}

		private static void MaybeLogClamp(float oldX, float oldZ, float newX, float newZ, IntSize mapSize, float allowanceX, float allowanceZ)
		{
			float now = Time.realtimeSinceStartup;
			if (now - _lastClampLogTime < ClampLogIntervalSeconds)
			{
				return;
			}

			_lastClampLogTime = now;
			VerificationLog(
				"CameraBounds",
				$"camera-position-clamped from=({oldX:0.0},{oldZ:0.0}) to=({newX:0.0},{newZ:0.0}) map={mapSize.width}x{mapSize.height} padding={CameraMapBoundsPadding:0.0} edgeAllowance=({allowanceX:0.0},{allowanceZ:0.0})");
		}
	}
}
}
