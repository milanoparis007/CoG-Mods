using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Entities;

public class MobileData : BaseData
{
	public PlayerID pid = PlayerID.System;

	public WorldPos worldpos;

	public float deg;

	public List<WorldPos> travelContext;

	public Fixnum health;

	public bool InTravel
	{
		get
		{
			if (travelContext != null)
			{
				return travelContext.Count > 0;
			}
			return false;
		}
	}

	public void Set(WorldPos wpos, float deg)
	{
		worldpos = wpos;
		this.deg = deg;
	}

	public void Clear()
	{
		Set(WorldPos.Zero, 0f);
	}
}
