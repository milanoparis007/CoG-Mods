using System.Collections.Generic;
using Game.Session.Data;

namespace Game.Services;

public sealed class FedsAdvisorConfig : AIAdvisorConfig
{
	public sealed class Investigation
	{
		public ModValue durationDayz;
	}

	public List<PhotoConfig> arrestPhotos;

	public Investigation investigation;
}
