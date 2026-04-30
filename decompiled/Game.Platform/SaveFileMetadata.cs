using System;
using System.Globalization;
using Game.Core;
using SomaSim.SION;

namespace Game.Platform;

public class SaveFileMetadata
{
	public bool autosave;

	public string slotId;

	public string mapName;

	public string playerName;

	public string groupName;

	public Label eth;

	public int crew;

	public int corners;

	public int money;

	public uint rngseed;

	public DateTime gameTime;

	public DateTime saveTime;

	public uint gameVersion;

	public SaveFileMetadata()
	{
	}

	public SaveFileMetadata(bool autosave, string mapName, string playerName, string groupName, int crew, int corners, int money, uint rngseed, DateTime gameTime, DateTime saveTime, Label eth)
	{
		this.autosave = autosave;
		this.mapName = mapName;
		this.playerName = playerName;
		this.groupName = groupName;
		this.crew = crew;
		this.corners = corners;
		this.money = money;
		this.rngseed = rngseed;
		this.gameTime = gameTime;
		this.saveTime = saveTime;
		this.eth = eth;
		slotId = (autosave ? "autosave" : saveTime.Ticks.ToString("D20", CultureInfo.InvariantCulture));
		gameVersion = GameSettings.version;
	}

	public static string Serialize(SaveFileMetadata data)
	{
		return SION.Print(Game.serv.serializer.CloneInstance().Serialize(data));
	}

	public static SaveFileMetadata Deserialize(string dataStr)
	{
		object source = SION.Parse(dataStr);
		return Game.serv.serializer.CloneInstance().Deserialize<SaveFileMetadata>(source);
	}
}
