using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Player;

public sealed class PlayerVizData
{
	public sealed class MeetingInfo
	{
		public PlayerID pid;

		public SimTime time;
	}

	public Dictionary<PlayerID, MeetingInfo> meetings = new Dictionary<PlayerID, MeetingInfo>();
}
