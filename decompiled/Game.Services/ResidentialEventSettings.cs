using System.Collections.Generic;
using Game.Core;

namespace Game.Services;

public sealed class ResidentialEventSettings
{
	public int resSearchNearbyCorners;

	public List<ResidentialEventConfig> events;

	public List<ResidentialEventResultConfig> results;

	public ResidentialEventConfig FindEvent(Label id)
	{
		foreach (ResidentialEventConfig @event in events)
		{
			if (@event.id == id)
			{
				return @event;
			}
		}
		return null;
	}

	public ResidentialEventResultConfig FindResult(Label id)
	{
		foreach (ResidentialEventResultConfig result in results)
		{
			if (result.id == id)
			{
				return result;
			}
		}
		return null;
	}

	internal void Validate()
	{
		foreach (ResidentialEventConfig @event in events)
		{
			foreach (Label key in @event.results.Keys)
			{
				_ = key;
			}
		}
		foreach (ResidentialEventResultConfig result in results)
		{
			_ = result.quest;
		}
	}
}
