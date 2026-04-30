using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public static class GangInfoPanelUtil
{
	private const string GANG_PANEL_BG = "BG/BG Highlight";

	private const string GANG_HEADER = "Header";

	private const string GANG_LEADER_PANEL = "Leader";

	private const string GANG_LSTANCE = "Stance";

	private const string GANG_LTRAITS = "Traits";

	private const string GANG_RELS_PANEL = "Rel";

	private const string GANG_RLEGEND = "Legend";

	private const string GANG_RVISIT = "Visit";

	private const string GANG_RRELS = "Relationship";

	private const string GANG_RBTN = "Person Info Button";

	public static (bool isAI, PlayerInfo player) IsConvoWithGangOrGoon(Entity peep)
	{
		PlayerInfo playerInfo = (peep?.data.agent.pid ?? PlayerID.INVALID).FindPlayer();
		return (isAI: playerInfo?.IsGangOrGoon ?? false, player: playerInfo);
	}

	public static void ShowGangPanel(GameObject panel, bool show, PlayerInfo player, Entity peep, Action<Entity> fn)
	{
		panel.SetActive(show);
		if (!show)
		{
			return;
		}
		Color color = player.territory.colorInfo.GetPlayerColor().SetAlpha(0.25f);
		panel.GetImage("BG/BG Highlight").color = color;
		bool flag = player.social.IsBoss(peep);
		panel.SetActive("Leader", flag);
		panel.SetActive("Rel", !flag);
		string text = Loc.Get(player.IsJustGoon ? "convodialog.goon" : (flag ? "convodialog.leader" : "convodialog.crew"), "groupname", player.social.PlayerGroupName, "name", peep.data.person.ShortName);
		panel.SetText("Header", text);
		if (flag)
		{
			panel.SetText("Stance", ExplainStance(player, shortform: true));
			panel.SetText("Traits", ExplainPersonality(player, shortform: true));
		}
		else
		{
			Entity boss = player.social.GetPlayerPeep();
			PersonInfoUtil.UpdateRelationshipBar(panel.GetChild("Relationship"), boss);
			panel.SetText("Legend", Loc.Get("convodialog.rel-legend"));
			panel.SetText("Visit", Loc.Get("convodialog.rel-visit"));
			panel.GetButton("Person Info Button").onClick.SetListener(delegate
			{
				fn(boss);
			});
		}
		panel.ForceRebuildLayoutImmediate();
	}

	public static string ExplainPersonality(PlayerInfo player, bool shortform)
	{
		if (player.IsJustGoon)
		{
			bool flag = player.ai.goon.IsSpecial();
			string key = (flag ? "ui.aiicon.goonspecial" : "ui.aiicon.goonobstacle");
			if (shortform)
			{
				return Loc.Get(flag ? "convodialog.goonspecial.short" : "convodialog.goonobstacle.short", "icon", Loc.Get(key));
			}
			string key2 = (flag ? "convodialog.goonspecial.long" : "convodialog.goonobstacle.long");
			return Loc.Get("ui.iconline", "icon", Loc.Get(key), "text", Loc.Get(key2));
		}
		if (player.IsJustGang)
		{
			IEnumerable<NPCPersonalityAspect> enumerable = player.ai.EnumeratePersonalityAspects();
			if (shortform)
			{
				string text = enumerable.SelectToString((NPCPersonalityAspect def) => Loc.Get(def.locicon), " ");
				return Loc.Get("convodialog.personality.short", "icons", text);
			}
			List<string> first = enumerable.Select((NPCPersonalityAspect def) => Loc.Get(def.locicon)).ToList();
			List<string> second = enumerable.Select((NPCPersonalityAspect def) => Loc.Get(def.lockey)).ToList();
			IEnumerable<string> values = first.Zip(second, (string icon, string text3) => Loc.Get("ui.iconline", "icon", icon, "text", text3));
			string text2 = string.Join("\n", values);
			return Loc.Get("convodialog.personality.long", "items", text2);
		}
		return "";
	}

	public static string ExplainStance(PlayerInfo player, bool shortform)
	{
		Demand.State state = Game.ctx.simman.demands.FindOrNull(PlayerID.HumanPlayer, Demand.Target.MakeForPlayer(player.PID))?.state ?? Demand.State.Neutral;
		bool flag = player.ai.combat.IsAggroWithoutTruce(PlayerID.HumanPlayer);
		bool flag2 = !flag && player.ai.combat.IsAggroAndTruce(PlayerID.HumanPlayer);
		string demandStateIcon = Demand.GetDemandStateIcon(state);
		string text = ((flag && player.IsJustGang) ? "ui.stance.gang-aggro" : ((flag && player.IsJustGoon) ? "ui.stance.goon-aggro" : (flag2 ? "ui.stance.gang-truce" : null)));
		string text2 = ((text != null) ? Loc.Get(text) : "");
		if (shortform)
		{
			string text3 = demandStateIcon + " " + text2;
			return Loc.Get("convodialog.stance.short", "icons", text3);
		}
		SimTime time = (flag2 ? player.ai.combat.GetTruceExpirationIfExists(PlayerID.HumanPlayer).expires : default(SimTime));
		string demandStateText = Demand.GetDemandStateText(state);
		string text4 = (flag ? Loc.Get("convodialog.stance.angry") : (flag2 ? Loc.Get("convodialog.stance.truce", "date", Loc.FormatDateShort(time)) : ""));
		string text5 = Loc.Get("ui.iconline", "icon", demandStateIcon, "text", demandStateText);
		string text6 = ((text == null) ? "" : Loc.Get("ui.iconline", "icon", text2, "text", text4));
		string text7 = (text5 + "\n" + text6).Trim();
		return Loc.Get("convodialog.stance.long", "items", text7);
	}

	public static string GenerateMouseoverExplanation(Entity peep)
	{
		if (peep == null)
		{
			return null;
		}
		PlayerInfo item = IsConvoWithGangOrGoon(peep).player;
		string text = ExplainPersonality(item, shortform: false);
		string text2 = ExplainStance(item, shortform: false);
		return (text + "\n\n" + text2).Trim();
	}
}
