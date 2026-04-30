using Game.Core;
using UnityEngine;

namespace Game.Session.Setup;

public sealed class WaterRegion
{
	private WaterNode _startNode;

	private WaterNode _destNode;

	public Plane left;

	public Plane right;

	public Plane front;

	public Plane back;

	public Vector3 leftCenter;

	public Vector3 rightCenter;

	public float length;

	public bool AllowBridges => _startNode.allowBridges;

	public Vector3 Direction => (_destNode.pos - _startNode.pos).AsVector3XZ.normalized;

	public WaterRegion(WaterNode a, WaterNode b)
	{
		_startNode = a;
		_destNode = b;
		_destNode.parent = _startNode;
		PrecomputePlanes();
	}

	private void PrecomputePlanes()
	{
		Vector3 asVector3XZ = _startNode.pos.AsVector3XZ;
		Vector3 asVector3XZ2 = _destNode.pos.AsVector3XZ;
		Vector3 vector = asVector3XZ2 - asVector3XZ;
		Vector3 vector2 = vector;
		length = vector.magnitude;
		asVector3XZ -= vector2.normalized;
		asVector3XZ2 += vector.normalized;
		Vector3 normalized = Vector3.Cross(vector2.normalized, Vector3.up).normalized;
		float num = _startNode.width;
		float num2 = (_startNode.forceFixedWidth ? _startNode.width : _destNode.width);
		Vector3 vector3 = asVector3XZ + normalized * num / 2f;
		Vector3 vector4 = asVector3XZ - normalized * num / 2f;
		back = new Plane(vector2.normalized, asVector3XZ);
		front = new Plane(-vector.normalized, asVector3XZ2);
		Vector3 normalized2 = Vector3.Cross(vector.normalized, Vector3.up).normalized;
		Vector3 vector5 = asVector3XZ2 + normalized2 * num2 / 2f;
		Vector3 vector6 = asVector3XZ2 - normalized2 * num2 / 2f;
		Vector3 normalized3 = (vector5 - vector3).normalized;
		normalized3 = -Vector3.Cross(normalized3, Vector3.up);
		Vector3 normalized4 = (vector6 - vector4).normalized;
		normalized4 = Vector3.Cross(normalized4, Vector3.up);
		leftCenter = Vector3.Lerp(vector3, vector5, 0.5f);
		rightCenter = Vector3.Lerp(vector4, vector6, 0.5f);
		left = new Plane(normalized3, leftCenter);
		right = new Plane(normalized4, rightCenter);
	}

	public bool IsPointWithin(WorldPos pos)
	{
		Vector3 asVector3XZ = pos.AsVector3XZ;
		if (back.GetSide(asVector3XZ) && front.GetSide(asVector3XZ) && left.GetSide(asVector3XZ))
		{
			return right.GetSide(asVector3XZ);
		}
		return false;
	}
}
