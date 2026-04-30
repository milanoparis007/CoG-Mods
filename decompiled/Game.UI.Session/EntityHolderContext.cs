using System;
using Game.Core;
using Game.Session.Entities;
using UnityEngine;

namespace Game.UI.Session;

public sealed class EntityHolderContext : MonoBehaviour
{
	public Func<EntityID> TargetEntityGetter;

	public void Set(Func<EntityID> fn)
	{
		TargetEntityGetter = fn;
	}

	public Entity GetTargetEntityOrNull()
	{
		return TargetEntityGetter().FindEntity();
	}
}
