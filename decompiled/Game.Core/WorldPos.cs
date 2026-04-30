using System;
using System.Diagnostics;
using UnityEngine;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct WorldPos : IEquatable<WorldPos>
{
	public const float MAGNITUDE_EPSILON = 0.001f;

	public static readonly WorldPos NORTH = new WorldPos(0f, 1f);

	public float x;

	public float y;

	public static readonly WorldPos Zero = new WorldPos(0f, 0f);

	public Vector3 AsVector3XZ => new Vector3(x, 0f, y);

	public Vector2 AsVector2 => new Vector2(x, y);

	public bool IsZero
	{
		get
		{
			if (x == 0f)
			{
				return y == 0f;
			}
			return false;
		}
	}

	public bool IsNan
	{
		get
		{
			if (!float.IsNaN(x))
			{
				return float.IsNaN(y);
			}
			return true;
		}
	}

	public float MagnitudeSquared => x * x + y * y;

	public float Magnitude => Mathf.Sqrt(x * x + y * y);

	public float Angle
	{
		get
		{
			float num = Mathf.Atan2(y, x);
			if (num < 0f)
			{
				num += (float)Math.PI * 2f;
			}
			return num * 57.29578f;
		}
	}

	public WorldPos Normalized => this / Magnitude;

	private string DebugString => ToString();

	public WorldPos(float x, float y)
	{
		this.x = x;
		this.y = y;
	}

	public WorldPos(Vector3 pos)
	{
		x = pos.x;
		y = pos.z;
	}

	public WorldPos(Vector2 pos)
	{
		x = pos.x;
		y = pos.y;
	}

	public WorldPos Increment(float dx, float dy)
	{
		return new WorldPos(x + dx, y + dy);
	}

	public WorldPos Rotate(float deg)
	{
		float f = (0f - deg) * ((float)Math.PI / 180f);
		float num = Mathf.Sin(f);
		float num2 = Mathf.Cos(f);
		return new WorldPos(num2 * x - num * y, num * x + num2 * y);
	}

	public static float Dot(WorldPos a, WorldPos b)
	{
		return a.x * b.x + a.y * b.y;
	}

	public WorldPos WithMagnitude(float mag)
	{
		return this * (mag / Magnitude);
	}

	public WorldPos Perpendicular(bool ccw = true)
	{
		if (!ccw)
		{
			return new WorldPos(y, 0f - x);
		}
		return new WorldPos(0f - y, x);
	}

	public static WorldPos ClampMagnitude(WorldPos pos, float maxLength)
	{
		if (pos.MagnitudeSquared > maxLength * maxLength)
		{
			return pos.WithMagnitude(maxLength);
		}
		return pos;
	}

	public static WorldPos Lerp(WorldPos a, WorldPos b, float t)
	{
		return new WorldPos(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
	}

	public static float? GetFacingDegrees(WorldPos current, WorldPos lookat)
	{
		if (current.EqualsEpsilon(lookat))
		{
			return null;
		}
		WorldPos worldPos = lookat - current;
		worldPos.y = 0f - worldPos.y;
		return worldPos.Angle + 90f;
	}

	public static WorldPos operator +(WorldPos a, WorldPos b)
	{
		return new WorldPos(a.x + b.x, a.y + b.y);
	}

	public static WorldPos operator -(WorldPos a, WorldPos b)
	{
		return new WorldPos(a.x - b.x, a.y - b.y);
	}

	public static WorldPos operator *(WorldPos a, float scale)
	{
		return new WorldPos(a.x * scale, a.y * scale);
	}

	public static WorldPos operator /(WorldPos a, float divisor)
	{
		return new WorldPos(a.x / divisor, a.y / divisor);
	}

	public static bool operator ==(WorldPos a, WorldPos b)
	{
		return a.Equals(b);
	}

	public static bool operator !=(WorldPos a, WorldPos b)
	{
		return !a.Equals(b);
	}

	public bool Equals(WorldPos other)
	{
		if (x == other.x)
		{
			return y == other.y;
		}
		return false;
	}

	public bool EqualsEpsilon(WorldPos other, float epsilon = 0.001f)
	{
		return (this - other).MagnitudeSquared < epsilon * epsilon;
	}

	public override bool Equals(object obj)
	{
		if (obj is WorldPos)
		{
			return this == (WorldPos)obj;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return x.GetHashCode() ^ y.GetHashCode();
	}

	public override string ToString()
	{
		return $"W({x},{y})";
	}
}
