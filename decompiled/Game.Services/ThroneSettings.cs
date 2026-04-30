using System.Collections.Generic;
using Game.Core;

namespace Game.Services;

public sealed class ThroneSettings
{
	public sealed class ThroneType
	{
		public Label id;

		public string loctitle;

		public string locdesc;

		public string locdefaultsig;

		public string Image;

		public string selectionBG;
	}

	public List<ThroneType> throneTypes = new List<ThroneType>();

	public List<Trophy> trophies = new List<Trophy>();

	public Trophy GetTrophyForId(Label id)
	{
		foreach (Trophy trophy in trophies)
		{
			if (trophy.id == id)
			{
				return trophy;
			}
		}
		return null;
	}

	public ThroneType GetThroneTypeForId(Label id)
	{
		foreach (ThroneType throneType in throneTypes)
		{
			if (throneType.id == id)
			{
				return throneType;
			}
		}
		return null;
	}
}
