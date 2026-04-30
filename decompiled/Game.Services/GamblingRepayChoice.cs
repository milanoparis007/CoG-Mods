using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Services;

public sealed class GamblingRepayChoice
{
	public Label id;

	public List<Label> idlist;

	public Fixnum success;

	public Fixnum appearChance;

	public IEnumerable<Label> GetAllIds()
	{
		if (idlist != null)
		{
			foreach (Label item in idlist)
			{
				yield return item;
			}
		}
		if (id.IsSet)
		{
			yield return id;
		}
	}
}
