using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.Commands;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public class HistoryTabSubview : PersonInfoSubview
{
	private struct HistoryLine
	{
		public string message;

		public SimTime date;
	}

	private const string GO_TEMPLATE = "Templates/AI Debug Element";

	private const string SCROLLVIEW = "Scroll View/Viewport/Content";

	private const string TEXT = "Scroll View/Viewport/Content/Text";

	public bool ShowDebug => Game.settings.IsEditor;

	public HistoryTabSubview(GameObject go, PersonInfoController controller, PanelType type, string locicon)
		: base(go, "Panel History", controller, type, locicon)
	{
	}

	public override void RefreshSubview()
	{
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		ShowSocialActions(sb);
		ShowDebugCurrentCommand(sb);
		ShowDebugActionHistory(sb);
		string text = sb.ToStringAndReturnToPool();
		panel.SetText("Scroll View/Viewport/Content/Text", text);
	}

	private void ShowSocialActions(StringBuilder sb)
	{
		sb.AppendLine(Loc.Get("ui.subview.history.header"), 2);
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(Model.entity.Id);
		List<HistoryLine> list = new List<HistoryLine>();
		if (listOrNull != null)
		{
			foreach (Relationship datum in listOrNull.data)
			{
				MakeRelInfos(list, datum);
			}
		}
		if (list.Count == 0)
		{
			sb.AppendLine(Loc.Get("ui.subview.history.none"));
			return;
		}
		List<HistoryLine> list2 = list.OrderByDescending((HistoryLine e) => e.date.days).ToList();
		while (list2.Count > 100)
		{
			list2.RemoveLast();
		}
		foreach (HistoryLine item in list2)
		{
			sb.AppendLine(item.message, 2);
		}
	}

	private void MakeRelInfos(List<HistoryLine> results, Relationship rel)
	{
		if (rel.socialhistory == null)
		{
			return;
		}
		IEnumerable<SocialActionInfo> history = rel.socialhistory.GetHistory();
		RelationshipSettings relationships = Game.serv.globals.settings.people.social.relationships;
		foreach (SocialActionInfo item in history)
		{
			SocialActionDef socialActionDefinition = relationships.GetSocialActionDefinition(item.defid);
			if (socialActionDefinition != null)
			{
				string text = MakeActionDef(socialActionDefinition, item);
				string text2 = Loc.FormatDateLong(item.started);
				string text3 = MakeExpiration(socialActionDefinition, item);
				string message = Loc.Get("ui.subview.history.rel-info", "date", text2, "desc", text, "expiration", text3);
				results.Add(new HistoryLine
				{
					date = item.started,
					message = message
				});
			}
		}
		string MakeActionDef(SocialActionDef def, SocialActionInfo entry)
		{
			EntityID to = rel.to;
			EntityID entityCtx = entry.entityCtx;
			string text4 = ((entityCtx.IsValid && entityCtx != to) ? NameUtils.GetGroupOrPeepName(entityCtx) : "?");
			string groupOrPeepName = NameUtils.GetGroupOrPeepName(to);
			return Loc.Get(def.locentry, "actor", groupOrPeepName, "target", text4);
		}
		static string MakeExpiration(SocialActionDef _, SocialActionInfo entry)
		{
			string text4 = Loc.FormatDateLong(entry.expires);
			int deltadays = (entry.expires - Game.ctx.clock.Now).deltadays;
			int num = Game.ctx.clock.DaysToTurnsRoundedUp(deltadays);
			return Loc.Get("ui.subview.history.expire-info", "date", text4, "days", deltadays, "turns", num);
		}
	}

	private void ShowDebugCurrentCommand(StringBuilder sb)
	{
		if (!ShowDebug)
		{
			return;
		}
		PlayerInfo player = Model.entity.components.agent.GetPlayer();
		if (player == null)
		{
			return;
		}
		using ListPool<Command>.PooledBlockList pooledBlockList = ListPool<Command>.Allocate();
		player.commands.PopulateListWithCommands(Model.entity.Id, pooledBlockList);
		if (pooledBlockList.Count > 0)
		{
			sb.AppendLine("\nDEBUG: ACTIVE COMMAND QUEUE");
			{
				foreach (Command item in pooledBlockList)
				{
					MakeCommandInfo(sb, item);
				}
				return;
			}
		}
		sb.AppendLine("\nDEBUG: ACTIVE COMMAND QUEUE: EMPTY");
	}

	private void MakeCommandInfo(StringBuilder sb, Command cmd)
	{
		string text = $"command = {cmd} ";
		if (!(cmd is CommandAttack commandAttack))
		{
			if (!(cmd is CommandGoto commandGoto))
			{
				if (cmd is CommandScopeOut commandScopeOut)
				{
					text += $", target = {commandScopeOut.nodeId.FindNode()}";
				}
			}
			else
			{
				text += $", target = {commandGoto.goalID.FindNode()}";
			}
		}
		else
		{
			text += $", target = {commandAttack.target.GetPeep()}";
		}
		sb.AppendLine(text);
	}

	private void ShowDebugActionHistory(StringBuilder sb)
	{
		if (!ShowDebug)
		{
			return;
		}
		List<ActionHistoryElement> actionHistory = Model.entity.data.agent.actionHistory;
		if (actionHistory == null || actionHistory.Count == 0)
		{
			return;
		}
		sb.AppendLine("\nDEBUG: ACTION HISTORY");
		foreach (ActionHistoryElement item in actionHistory)
		{
			if (item.ctx != "")
			{
				string text = $"{item.turn} {item.type}: {item.ctx}";
				sb.AppendLine("<indent=20>" + text + "</indent>");
			}
		}
	}
}
