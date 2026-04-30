using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Actions;

public class ActionNavigate : GameAction
{
	public struct State
	{
		public PlayerID pid;

		public List<WorldPos> path;

		public NodeID fuzztarget;

		public bool backwards;

		public bool usesProportionalMovement;

		public float movementGain;

		public float movementMax;

		public float rotationGain;

		public float rotationMax;
	}

	public State state;

	private const float MAX_DELTA_TIME = 0.1f;

	private const float MAX_DELTA_TIME_15FPS = 0.066f;

	private WorldPos _now;

	private WorldPos _last;

	private WorldPos _target;

	private static List<Vector2> _tmpOffsets = new List<Vector2>();

	public ActionNavigate()
	{
	}

	public ActionNavigate(PlayerID pid, List<WorldPos> path, Node fuzztarget = null, bool reversed = false)
	{
		state.pid = pid;
		state.path = path;
		state.fuzztarget = fuzztarget?.id ?? NodeID.INVALID;
		state.backwards = reversed;
	}

	internal override void OnStart(bool loaded)
	{
		base.OnStart(loaded);
		_target = (_now = (_last = base.Agent.data.mobile.worldpos));
		if (loaded)
		{
			return;
		}
		if (state.path == null)
		{
			Stop(success: false);
			return;
		}
		state.usesProportionalMovement = base.Agent.config.mobile.usesGain;
		if (state.usesProportionalMovement)
		{
			MobileConfig mobile = base.Agent.config.mobile;
			state.movementGain = mobile.movementGain;
			state.movementMax = mobile.movementMax;
			state.rotationGain = mobile.rotationGain;
			state.rotationMax = mobile.rotationMax;
		}
		PreprocessAddOffsets(state.path, base.Agent, state.fuzztarget.FindNode(), state.backwards);
		base.Agent.components.mobile.HandleTravelStart(state.path);
	}

	internal override void OnStop()
	{
		base.Agent.components.mobile.HandleTravelEnd();
		base.OnStop();
	}

	internal override void OnUpdate()
	{
		base.OnUpdate();
		if (state.path != null && state.path.Count > 0)
		{
			FollowPath();
		}
		else
		{
			Stop(success: true);
		}
	}

	private void FollowPath()
	{
		if (state.path == null)
		{
			return;
		}
		float num = Game.ctx.clock.LastGameAnimUpdate.frameDeltaSeconds;
		if (num <= 0f)
		{
			return;
		}
		if (num > 0.1f)
		{
			num = 0.1f;
		}
		if (state.usesProportionalMovement && num > 0.066f)
		{
			num = 0.066f;
		}
		float velocity = base.Agent.components.mobile.GetVelocity(state.pid);
		_target = FindNextTargetPosition(velocity, num, _target);
		_now = base.Agent.data.mobile.worldpos;
		if (_now.IsNan || _target.IsNan)
		{
			Stop(success: false);
			return;
		}
		if (state.usesProportionalMovement)
		{
			ProportionalFollowMovementTarget(num);
		}
		else
		{
			base.Agent.components.mobile.Move(_now, _target);
		}
		_last = _now;
		if (state.path.Count == 0)
		{
			Stop(success: true);
		}
	}

	private WorldPos FindNextTargetPosition(float velocity, float deltatime, WorldPos current)
	{
		WorldPos result = current;
		while (state.path.Count > 0)
		{
			WorldPos worldPos = state.path.LastOrDefaultFast();
			float num = (worldPos - current).Magnitude / velocity;
			if (deltatime >= num)
			{
				deltatime -= num;
				current = (result = worldPos);
				state.path.RemoveLast();
				continue;
			}
			float t = deltatime / num;
			return WorldPos.Lerp(current, worldPos, t);
		}
		return result;
	}

	private void ProportionalFollowMovementTarget(float deltatime)
	{
		WorldPos worldPos = _now - _last;
		WorldPos delta = _target - _now;
		float num = Mathf.Clamp(FindAngle(worldPos, delta) * state.rotationGain, 0f - state.rotationMax, state.rotationMax);
		WorldPos worldPos2 = RotateAtVelocity(worldPos, num * deltatime);
		if (worldPos2.IsZero)
		{
			worldPos2 = delta.Normalized;
		}
		float num2 = Mathf.Clamp(delta.Magnitude * state.movementGain, 0f, state.movementMax);
		WorldPos worldPos3 = worldPos2 * (num2 * deltatime);
		WorldPos to = _now + worldPos3;
		base.Agent.components.mobile.Move(_now, to);
	}

	private static float FindAngle(WorldPos movement, WorldPos delta)
	{
		if (movement.IsZero || delta.IsZero)
		{
			return 0f;
		}
		return Vector2.SignedAngle(movement.Normalized.AsVector2, delta.Normalized.AsVector2) * ((float)Math.PI / 180f);
	}

	private static WorldPos RotateAtVelocity(WorldPos original, float theta)
	{
		if (original.IsZero)
		{
			return original;
		}
		WorldPos normalized = original.Normalized;
		float num = Mathf.Sin(theta);
		float num2 = Mathf.Cos(theta);
		return new WorldPos(normalized.x * num2 - normalized.y * num, normalized.y * num2 + normalized.x * num);
	}

	public static void PreprocessAddOffsets(List<WorldPos> path, Entity vehicle, Node target, bool backwards = false)
	{
		PreprocessAddOffset(path, vehicle, !backwards);
		if (target != null && vehicle.config.mobile.IsPeepVehicleType)
		{
			PreprocessFuzzEndPoint(path, target, vehicle);
		}
	}

	private static void PreprocessAddOffset(List<WorldPos> path, Entity vehicle, bool reverse)
	{
		float navOffsetTiles = vehicle.config.mobile.navOffsetTiles;
		_tmpOffsets.Clear();
		_tmpOffsets.Add(default(Vector2));
		int i = 1;
		for (int count = path.Count; i < count; i++)
		{
			Vector2 asVector = (path[i] - path[i - 1]).AsVector2;
			Vector2 to = ((i + 1 < count) ? (path[i + 1] - path[i]).AsVector2 : asVector);
			float num = Vector2.SignedAngle(asVector, to);
			if ((double)Math.Abs(num) < 0.1)
			{
				num = 0f;
			}
			Vector2 vector = new Vector2(navOffsetTiles, 0f);
			if (num < 0f)
			{
				vector += new Vector2(0f, 0f - navOffsetTiles);
			}
			else if (num > 0f)
			{
				vector += new Vector2(0f, navOffsetTiles);
			}
			float z = Vector2.SignedAngle(Vector2.up, asVector);
			Vector2 item = Quaternion.Euler(0f, 0f, z) * vector;
			_tmpOffsets.Add(item);
		}
		int j = 1;
		for (int count2 = path.Count; j < count2; j++)
		{
			path[j] = path[j].Increment(_tmpOffsets[j].x, _tmpOffsets[j].y);
		}
		if (reverse)
		{
			path.ReverseInPlace();
		}
	}

	private static void PreprocessFuzzEndPoint(IList<WorldPos> list, Node target, Entity entity)
	{
		SplitMix64 rng = new SplitMix64(entity.components.ident.hash ^ (uint)target.id.index);
		float dx = rng.Generate(-0.5f, 0.5f);
		float dy = rng.Generate(-0.5f, 0.5f);
		list.Insert(0, target.pos.Increment(dx, dy));
	}
}
