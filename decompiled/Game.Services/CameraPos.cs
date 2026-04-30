using UnityEngine;

namespace Game.Services;

public struct CameraPos
{
	public float x;

	public float z;

	public Vector3 AsVector3XZ => new Vector3(x, 0f, z);

	public CameraPos(float x, float z)
	{
		this.x = x;
		this.z = z;
	}

	public static CameraPos FromVectorXZ(Vector3 v)
	{
		return new CameraPos(v.x, v.z);
	}

	public Vector3 ToVector3XZ(float y)
	{
		return new Vector3(x, y, z);
	}
}
