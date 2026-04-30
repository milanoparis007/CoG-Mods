using System.Collections.Generic;
using Game.Core;
using Game.Session.Player;
using UnityEngine;

namespace Game.Session.Board;

public class TerritoryCollider : MonoBehaviour
{
	private PolygonCollider2D _collider;

	private PlayerID _id;

	public PlayerInfo GetPlayer()
	{
		return Game.ctx.players.WithID(_id);
	}

	public void Set(PlayerID id, List<WorldPos> line)
	{
		_id = id;
		if (_collider == null)
		{
			_collider = base.gameObject.AddComponent<PolygonCollider2D>();
		}
		int count = line.Count;
		Vector2[] array = new Vector2[count];
		Vector2 zero = Vector2.zero;
		foreach (WorldPos item in line)
		{
			zero += item.AsVector2;
		}
		zero /= (float)count;
		for (int i = 0; i < count; i++)
		{
			array[i] = line[i].AsVector2 - zero;
		}
		_collider.points = array;
		base.transform.position = new Vector3(zero.x, zero.y, 0f);
	}
}
