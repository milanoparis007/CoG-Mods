using Game.Core;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class StarterPack
{
	public sealed class Entry
	{
		public Fixnum cash;

		public LabelDictionary<Fixnum> resources;
	}

	public Entry crew;

	public Entry building;
}
