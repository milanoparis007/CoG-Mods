using System.Collections.Generic;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class GamblingGeneral
{
	public struct SizeEntry
	{
		public int min;

		public string key;
	}

	public ModValue minCorners;

	public ModValue casinosPerPrecinct;

	public ModValue weeklyCostModifier;

	public List<SizeEntry> sizeLabels;

	public string FindSizeLabelFor(Fixnum size)
	{
		foreach (SizeEntry sizeLabel in sizeLabels)
		{
			if (size >= sizeLabel.min)
			{
				return sizeLabel.key;
			}
		}
		return sizeLabels.LastOrDefaultFast().key;
	}
}
