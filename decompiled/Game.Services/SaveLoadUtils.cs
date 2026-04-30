using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Game.Session.Data;
using Game.Session.Player;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Services;

public static class SaveLoadUtils
{
	[DebuggerDisplay("{DebugString}")]
	public struct MemberProvider<T>
	{
		public MemberInfo member;

		public T provider;

		private string DebugString => member.Name + " => " + provider.GetType().Name;
	}

	private class SaveTaskData
	{
		public MemberProvider<ISaveLoadProvider> member;

		public ConcurrentSaveTable table;

		public void DoWork()
		{
			LogThread($"Saving {member.member.Name} of type {member.provider}");
			Stopwatch stopwatch = Stopwatch.StartNew();
			Serializer s = Game.serv.serializer.CloneInstance();
			member.provider.Save(s, table);
			stopwatch.Stop();
			LogThread($"Saving {member.member.Name} took {stopwatch.ElapsedMilliseconds} ms");
		}
	}

	public static IEnumerable<MemberProvider<T>> GetEachOfType<T>(object obj) where T : class
	{
		List<MemberInfo> membersByType = TypeUtils.GetMembersByType<T>(obj);
		foreach (MemberInfo item in membersByType)
		{
			if (TypeUtils.GetValue(item, obj) is T provider)
			{
				yield return new MemberProvider<T>
				{
					member = item,
					provider = provider
				};
			}
		}
	}

	public static void SaveMembersByNameInParallel(object obj, Hashtable result)
	{
		List<SaveTaskData> list = (from mp in GetEachOfType<ISaveLoadProvider>(obj)
			select new SaveTaskData
			{
				member = mp,
				table = new ConcurrentSaveTable()
			}).ToList();
		Stopwatch stopwatch = Stopwatch.StartNew();
		Parallel.ForEach(list, delegate(SaveTaskData datum)
		{
			datum.DoWork();
		});
		foreach (SaveTaskData item in list)
		{
			result[item.member.member.Name] = item.table.ToHashtable();
		}
		stopwatch.Stop();
		LogThread($"Parallel save members took {stopwatch.ElapsedMilliseconds} ms for {obj}");
	}

	public static IEnumerator LoadMembersByNameCoroutine(object obj, Hashtable data)
	{
		foreach (MemberProvider<ISaveLoadProvider> item in GetEachOfType<ISaveLoadProvider>(obj))
		{
			if (data.ContainsKey(item.member.Name) && data[item.member.Name] is Hashtable data2)
			{
				yield return Game.serv.sequencer.StartCoroutine(item.provider.Load(data2));
			}
		}
	}

	public static void DeserializeSingleKey<T>(Hashtable data, string key, Action<T> proc)
	{
		if (data.ContainsKey(key) && Game.serv.serializer.instance.Deserialize(data[key], typeof(T)) is T obj)
		{
			proc(obj);
		}
	}

	public static byte[] ObjectToBytes(object obj)
	{
		using MemoryStream memoryStream = new MemoryStream();
		new BinaryFormatter().Serialize(memoryStream, obj);
		return memoryStream.ToArray();
	}

	public static object BytesToObject(byte[] bytes)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryFormatter binaryFormatter = new BinaryFormatter();
		memoryStream.Write(bytes, 0, bytes.Length);
		return binaryFormatter.Deserialize(memoryStream);
	}

	public static void LogThread(string message)
	{
		_ = Thread.CurrentThread.ManagedThreadId;
	}

	public static void TryFixups()
	{
		uint savefileversion = Game.ctx.session.scenario.savefileversion;
		uint num = GameSettings.MakeVersion(1u, 1u, 3u);
		if (savefileversion < num)
		{
			RunFixupForMeetingSystemPlayer();
		}
		RunDLCFixups();
	}

	public static void RunFixupForMeetingSystemPlayer()
	{
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			Dictionary<PlayerID, PlayerVizData.MeetingInfo> allMeetingsUnsafe = item.meetings.GetAllMeetingsUnsafe();
			if (allMeetingsUnsafe.ContainsKey(PlayerID.System))
			{
				allMeetingsUnsafe.Remove(PlayerID.System);
			}
			foreach (CrewAssignment item2 in item.crew.AllCrew)
			{
				List<Relationship> data = Game.ctx.simman.rels.GetListOrNull(item2.peepId).data;
				List<Relationship> list = data.Where((Relationship x) => x.to.IsNotValid).ToList();
				if (list.Count == 0)
				{
					continue;
				}
				foreach (Relationship item3 in list)
				{
					data.Remove(item3);
				}
			}
		}
	}

	public static void RunDLCFixups()
	{
		PlayerInfo human = Game.ctx.players.Human;
		foreach (CrewAssignment item in Game.ctx.players.Human.crew.AllCrew)
		{
			if (item.GetPeep().data.agent.xp != null && !human.crew.CanSeeRole(item.GetPeep().data.agent.xp.GetCrewRole()))
			{
				human.crew.UnassignRole(item);
			}
		}
	}
}
