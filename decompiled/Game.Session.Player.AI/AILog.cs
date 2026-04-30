using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Player.AI;

public class AILog
{
	private class Sentinel : MonoBehaviour
	{
		private void OnApplicationQuit()
		{
			ProduceLog(decisions: true, entries: true, relationships: true);
		}
	}

	private class Decision
	{
		public readonly SimTime ts;

		public readonly PlayerID pid;

		public readonly AIAdvisor advisor;

		public readonly string adname;

		public readonly string bossname;

		public readonly string info;

		public readonly string debug;

		public Decision(PlayerID pid, AIAdvisor advisor, string info)
		{
			ts = Game.ctx.clock.Now;
			this.pid = pid;
			this.advisor = advisor;
			adname = advisor.GetType().Name.Replace("Advisor", "").TrimOrPad(11);
			bossname = pid.FindPlayer().social.PlayerLastName.TrimOrPad(10);
			this.info = info;
			debug = GetDate() + " " + pid.ToString().TrimOrPad(10) + " " + bossname + " " + adname + " " + info;
		}

		public string GetDate()
		{
			return ts.ToDate().ToString("yyyy/MM/dd");
		}
	}

	private class Entry
	{
		public enum Type
		{
			Milestone,
			Request,
			RuleScript,
			Problem
		}

		public readonly SimTime ts;

		public readonly Type type;

		public readonly bool success;

		public readonly PlayerID pid;

		public readonly EntityID eid;

		public readonly string scriptname;

		public readonly string info;

		public readonly string debug;

		public bool IsMilestone => type == Type.Milestone;

		public bool IsScript => type != Type.Milestone;

		public Entry(Type type, bool success, PlayerID pid, EntityID eid, string scriptname, string info)
		{
			ts = Game.ctx.clock.Now;
			this.type = type;
			this.success = success;
			this.pid = pid;
			this.eid = eid;
			this.scriptname = scriptname;
			this.info = info;
			DateTime dateTime = Game.ctx.clock.Now.ToDate();
			_ = $"{dateTime.Year}.{dateTime.Month:D2}.{dateTime.Day:D2}";
			string text = (success ? "OK  " : "fail");
			string text2 = (eid.IsValid ? eid.ToString() : "        ");
			string text3 = type switch
			{
				Type.Problem => "!PROBLEM", 
				Type.RuleScript => "(fallback)", 
				Type.Request => "", 
				Type.Milestone => "(milestone)", 
				_ => "?type?", 
			};
			debug = $"{GetDate()} {pid}\t{text} {text2}\t{scriptname}\t{text3}\t{info}";
		}

		public string GetDate()
		{
			return ts.ToDate().ToString("yyyy/MM/dd");
		}
	}

	private static AILog _instance;

	private List<Entry> entries;

	private List<Decision> decisions;

	public static AILog Instance
	{
		get
		{
			AILog obj = _instance ?? new AILog();
			_instance = obj;
			return obj;
		}
	}

	public AILog()
	{
		entries = new List<Entry>();
		decisions = new List<Decision>();
		if (Game.serv.globals.settings.general.debug.dumpAIStatsOnExit)
		{
			GameObject gameObject = new GameObject();
			gameObject.name = "AI Log Sentinel";
			gameObject.AddComponent<Sentinel>();
		}
	}

	public static void LogRuleScript(PlayerID pid, EntityID eid, string scriptName, string info = null)
	{
		LogInternal(new Entry(Entry.Type.RuleScript, success: true, pid, eid, scriptName, info));
	}

	public static void LogRequest(bool dispatched, PlayerID pid, AdvisorRequest req, string info = null)
	{
		LogInternal(new Entry(Entry.Type.Request, dispatched, pid, req.assignedTo, req.script.String, info));
	}

	public static void LogMilestone(PlayerID pid, EntityID eid, string info)
	{
		LogInternal(new Entry(Entry.Type.Milestone, success: true, pid, eid, "", info));
	}

	public static void LogProblem(PlayerID pid, EntityID eid, string info)
	{
		LogInternal(new Entry(Entry.Type.Problem, success: false, pid, eid, "", info));
	}

	public static void LogAIDecision(PlayerID pid, AIAdvisor advisor, string info)
	{
		LogInternal(new Decision(pid, advisor, info));
	}

	private static void LogInternal(Entry entry)
	{
		Instance.entries.Add(entry);
	}

	private static void LogInternal(Decision d)
	{
		Instance.decisions.Add(d);
	}

	public static string ProduceLog(bool decisions, bool entries, bool relationships)
	{
		if (decisions)
		{
			return ProduceDecisions();
		}
		if (entries)
		{
			return ProduceEntries();
		}
		if (relationships)
		{
			return ProduceRelationships();
		}
		return null;
	}

	private static string ProduceRelationships()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"AI RELATIONSHIPS\n\nTotal {Game.ctx.players.all.Count} players");
		stringBuilder.AppendLine("Game date: " + Loc.FormatDateLong(Game.ctx.clock.Now));
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			stringBuilder.AppendLine($"\n\n{item.PID} {item.social.PlayerGroupName}, type: {item.PlayerType}, boss: {item.social.PlayerFullName}");
			foreach (PlayerInfo item2 in Game.ctx.players.all)
			{
				Relationship relationshipFromPlayerTo = item.social.GetRelationshipFromPlayerTo(item2.PID);
				if (relationshipFromPlayerTo == null)
				{
					continue;
				}
				stringBuilder.AppendLine($"  => {item2.PID} {item2.social.PlayerFullName} ({item2.PlayerType}): rel = {relationshipFromPlayerTo.Evaluate().current}");
				SocialHistoryData historyOrNull = relationshipFromPlayerTo.GetHistoryOrNull();
				if (historyOrNull == null)
				{
					continue;
				}
				foreach (SocialActionInfo item3 in historyOrNull.GetHistory())
				{
					stringBuilder.AppendLine("      " + ExplainHistory(relationshipFromPlayerTo, item3));
				}
			}
		}
		return DebugFileUtil.ProduceDebugTextFile("airels", stringBuilder.ToString());
		static string ExplainHistory(Relationship rel, SocialActionInfo entry)
		{
			SocialActionDef socialActionDefinition = Game.serv.globals.settings.people.social.relationships.GetSocialActionDefinition(entry.defid);
			EntityID to = rel.to;
			EntityID entityCtx = entry.entityCtx;
			string text = ((entityCtx.IsValid && entityCtx != to) ? NameUtils.GetGroupOrPeepName(entityCtx) : "?");
			string groupOrPeepName = NameUtils.GetGroupOrPeepName(to);
			string text2 = Loc.Get(socialActionDefinition.locentry, "actor", groupOrPeepName, "target", text);
			string text3 = Loc.FormatDateShort(entry.expires);
			return text2 + "; expires " + text3;
		}
	}

	private static string ProduceDecisions()
	{
		List<Decision> list = Instance.decisions;
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"AI LOG\n\n{list.Count} decisions");
		stringBuilder.AppendLine("\n\nPLAYER BUILDINGS AND MODULES");
		foreach (PlayerInfo item in Game.ctx.players.all.Where((PlayerInfo pp) => pp.IsJustGang || pp.IsHuman))
		{
			string arg = string.Join(", ", from s in item.skills.GetCurrentSkills()
				select s.id.String);
			stringBuilder.AppendLine($"{item.PID}: {arg}");
			foreach (EntityID item2 in item.territory.GetAllControlledBuildingsUnsafe())
			{
				Entity entity = item2.FindEntity();
				Entity entity2 = BuildingUtil.FindBizForBuilding(entity);
				IEnumerable<Label> values = from m in ModulesUtil.GetBizModules(item2)
					select m.ModuleConfig.Id;
				string text = string.Join(", ", values);
				stringBuilder.AppendLine($"{item.PID}: {entity} {entity2} => {text}");
			}
		}
		stringBuilder.AppendLine("\n\nPER PLAYER HISTORY");
		foreach (IGrouping<short, Decision> item3 in from e in list
			group e by e.pid.id into e
			orderby e.Key
			select e)
		{
			stringBuilder.AppendLine($"\nPID {item3.Key}:");
			foreach (Decision item4 in item3)
			{
				stringBuilder.AppendLine(item4.debug);
			}
		}
		stringBuilder.AppendLine("\n\nFULL CHRONOLOGICAL LOG");
		foreach (Decision item5 in list)
		{
			stringBuilder.AppendLine(item5.debug);
		}
		string text2 = stringBuilder.ToString();
		return DebugFileUtil.ProduceDebugTextFile("aidecisions", text2);
	}

	private static string ProduceEntries()
	{
		List<Entry> list = Instance.entries;
		List<Entry> list2 = list.Where((Entry e) => e.IsScript && e.success).ToList();
		List<Entry> list3 = list.Where((Entry e) => e.IsScript && !e.success).ToList();
		List<Entry> list4 = list.Where((Entry e) => e.IsMilestone).ToList();
		StringBuilder sb = new StringBuilder();
		sb.AppendLine($"AI LOG\n\n{list.Count} entries; {list4.Count} milestones, {list2.Count} assigned, {list3.Count} dropped");
		sb.AppendLine("\n\nSCRIPTS DISPATCHED");
		(from e in list2
			group e by e.scriptname).ToList().ForEach(delegate(IGrouping<string, Entry> e)
		{
			sb.AppendLine($"{e.Count():D5}: {e.Key}");
		});
		sb.AppendLine("\n\nSCRIPTS DROPPED");
		(from e in list3
			group e by e.scriptname).ToList().ForEach(delegate(IGrouping<string, Entry> e)
		{
			sb.AppendLine($"{e.Count():D5}: {e.Key}");
		});
		sb.AppendLine("\n\nALL MILESTONES LOG");
		list4.ForEach(delegate(Entry e)
		{
			sb.AppendLine(e.debug);
		});
		sb.AppendLine("\n\nPER PLAYER HISTORY");
		foreach (IGrouping<short, Entry> item in from e in list
			group e by e.pid.id into e
			orderby e.Key
			select e)
		{
			sb.AppendLine($"\nPID {item.Key}:");
			foreach (Entry item2 in item)
			{
				sb.AppendLine(item2.debug);
			}
		}
		sb.AppendLine("\n\nFULL CHRONOLOGICAL LOG");
		foreach (Entry item3 in list)
		{
			sb.AppendLine(item3.debug);
		}
		string text = sb.ToString();
		return DebugFileUtil.ProduceDebugTextFile("aievents", text);
	}
}
