using System;
using System.Globalization;
using Game.Platform;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public class SaveLoadCardData
{
	public int index;

	public IPlatformSaveSlotDescriptor slot;

	public SaveFileMetadata metadata;

	public string message;

	public string description;

	public Toggle toggle;

	public GameObject card;

	public bool IsNewGame => slot == null;

	public bool CanLoad => slot != null;

	public bool CanDelete => slot != null;

	public bool IsVersionCompatible
	{
		get
		{
			if (metadata != null)
			{
				return Game.settings.IsSaveFileVersionCompatible(metadata.gameVersion);
			}
			return false;
		}
	}

	public SaveLoadCardData(IPlatformSaveSlotDescriptor slot)
		: this(0, slot, slot.Metadata)
	{
	}

	public SaveLoadCardData(SaveFileMetadata meta)
		: this(0, null, meta)
	{
	}

	private SaveLoadCardData(int index, IPlatformSaveSlotDescriptor slot, SaveFileMetadata metadata)
	{
		this.index = index;
		this.slot = slot;
		this.metadata = metadata;
		string text = (IsNewGame ? Loc.Get("ui.saveload.autosave.new") : (metadata.autosave ? Loc.Get("ui.saveload.autosave.autosave") : ""));
		string text2 = (metadata.eth.IsSet ? Loc.Get(Game.serv.globals.settings.ethnicities.FindEthnicityDef(metadata.eth).loc.icon) : "");
		string text3 = ((!metadata.eth.IsSet) ? "" : (PlayerCrew.HasEthPackForEth(metadata.eth) ? (text2 + " ") : ""));
		message = Loc.Get("ui.saveload.savecard", "ethFlag", text3, "prefix", text, "playername", metadata.playerName, "mapname", metadata.mapName, "mapdate", metadata.gameTime.ToLongDateString()).Trim();
		string text4 = (IsVersionCompatible ? "" : Loc.Get("ui.saveload.warning.formatting", "message", Loc.Get("ui.saveload.warning", "version", GameSettings.GetVersionString(metadata.gameVersion))));
		DateTime dateTime = metadata.saveTime.ToLocalTime();
		string text5 = Loc.Get("ui.saveload.timestring", "savedate", dateTime.ToShortDateString(), "savetime", dateTime.ToShortTimeString(), "warning", text4).Trim();
		string text6 = Loc.Get("ui.saveload.playerdesc", "groupname", metadata.groupName, "cash", Loc.Money(metadata.money), "people", Loc.FormatNumber(metadata.crew), "corners", Loc.FormatNumber(metadata.corners));
		string text7 = Loc.Get("ui.customgame.forceseed");
		if (metadata.rngseed != 0)
		{
			_ = "\n" + text7 + ": " + metadata.rngseed.ToString("D", CultureInfo.InvariantCulture);
		}
		description = text6 + "\n\n" + text5;
	}

	public void SetCard(GameObject card)
	{
		this.card = card;
		toggle = card.GetChildToggle();
		toggle.isOn = false;
	}
}
