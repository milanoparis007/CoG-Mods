using System.Collections.Generic;

namespace Game.Services;

public class RemoteSettingsDefinition
{
	public enum AnnouncementButtons
	{
		Ok,
		Steam
	}

	public class Announcement
	{
		public string id;

		public string loc;

		public int min;

		public int mod;

		public List<string> langs;

		public AnnouncementButtons buttons;
	}

	public int logmod;

	public bool logerrors;

	public int minver;

	public List<Announcement> announcements;

	public bool isValidForPlayer(int userid)
	{
		if (logmod != 0 && userid % logmod == 0)
		{
			return GameSettings.version >= minver;
		}
		return false;
	}
}
