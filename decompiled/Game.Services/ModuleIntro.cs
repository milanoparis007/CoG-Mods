using Game.Core;

namespace Game.Services;

public sealed class ModuleIntro
{
	public class Locs
	{
		public string unknownConsumes;

		public string unknownProduces;

		public string knownConsumes;

		public string knownProduces;
	}

	public TagList tags;

	public Locs locs;
}
