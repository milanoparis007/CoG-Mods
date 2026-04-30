using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Entities;

public class UpgradeData
{
	public EntityID source;

	public Dictionary<Type, object> databag = new Dictionary<Type, object>();

	public void Reset()
	{
		source = 0uL;
		databag.Clear();
	}
}
