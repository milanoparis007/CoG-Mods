using System;

namespace Game.Services.Filesystem;

public sealed class UserStats
{
	public string uuid = Guid.NewGuid().ToString().Substring(0, 8);

	public int session;

	public bool IsFirstSession => session == 1;
}
