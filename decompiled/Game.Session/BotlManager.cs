using System;
using System.IO;
using System.Reflection;
using System.Text;
using BotL;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session;

public class BotlManager : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	private class DebugWriter : StringWriter
	{
		public int lastlen;

		public void TrySendToDebug()
		{
			StringBuilder stringBuilder = GetStringBuilder();
			int length = stringBuilder.Length;
			if (length > lastlen)
			{
				stringBuilder.ToString(lastlen, length - lastlen);
				lastlen = length;
			}
		}
	}

	public GlobalVariable islogging;

	public GlobalVariable arg1;

	public GlobalVariable arg2;

	public GlobalVariable arg3;

	public GlobalVariable label;

	public GlobalVariable business;

	public GlobalVariable entity;

	public GlobalVariable entityid;

	public GlobalVariable humanpeepid;

	public GlobalVariable socialaction;

	public GlobalVariable result;

	private const bool DEFAULT_LOGGING_VALUE = false;

	private static DebugWriter _logWriter;

	private static bool _isKbCompiled;

	private bool _isInitialized;

	public override bool IsInitializingDone => _isInitialized;

	public static void Log(params object[] data)
	{
		_logWriter.WriteLine("BOTL: " + string.Join(" ", data));
	}

	public override void OnInitializeStarted()
	{
		base.OnInitializeStarted();
		DoStaticInitialize(this);
	}

	private static void DoStaticInitialize(BotlManager manager)
	{
		if (_isKbCompiled)
		{
			manager.OnAfterStaticInitialize();
			return;
		}
		BotL.TypeUtils.AddTypeSearchPath("Game", "Assembly-CSharp");
		BotL.TypeUtils.AddTypeSearchPath("Game.Core", "Assembly-CSharp");
		BotL.TypeUtils.AddTypeSearchPath("Game.Session", "Assembly-CSharp");
		BotL.TypeUtils.AddTypeSearchPath("Game.Session.Entities", "Assembly-CSharp");
		BotL.TypeUtils.AddTypeSearchPath("Game.Session.Sim", "Assembly-CSharp");
		BotL.TypeUtils.AddTypeSearchPath("Game.Session.Sim.Modules", "Assembly-CSharp");
		BotL.TypeUtils.AddTypeSearchPath("Game.Services", "Assembly-CSharp");
		StreamingAssetsUtil.LoadAsString("Queries/main", ResourceType.BotlFile, delegate(string contents)
		{
			try
			{
				StaticInitializeAllRegisters();
				StaticInitializeLogging();
				KB.Compile(contents);
				_isKbCompiled = true;
			}
			catch (Exception ex)
			{
				Logger.Error("BotlManager exception: " + ex);
			}
			manager.OnAfterStaticInitialize();
		});
	}

	private void OnAfterStaticInitialize()
	{
		try
		{
			InitializeAllRegisters();
			InitializeLogging();
		}
		catch (Exception ex)
		{
			Logger.Error("BotlManager exception: " + ex);
		}
		_isInitialized = true;
	}

	public override void OnInteractive()
	{
		base.OnInteractive();
		humanpeepid.Value.SetGeneral(Game.ctx.players.Human.social.PlayerPeepId);
	}

	private static void StaticInitializeAllRegisters()
	{
		foreach (MemberInfo item in SomaSim.Util.TypeUtils.GetMembersByType<GlobalVariable>(typeof(BotlManager)))
		{
			GlobalVariable.DefineGlobal(item.Name.ToLower(), -1);
		}
	}

	private void InitializeAllRegisters()
	{
		foreach (MemberInfo item in SomaSim.Util.TypeUtils.GetMembersByType<GlobalVariable>(this))
		{
			GlobalVariable value = GlobalVariable.Find(item.Name.ToLower());
			((FieldInfo)item).SetValue(this, value);
		}
	}

	public override void OnReleased()
	{
		foreach (GlobalVariable memberInstance in SomaSim.Util.TypeUtils.GetMemberInstances<GlobalVariable>(this))
		{
			memberInstance.Value.SetGeneral(null);
		}
		base.OnReleased();
	}

	public bool IsTrue(string query)
	{
		try
		{
			return KB.IsTrue(query);
		}
		catch (Exception ex)
		{
			Logger.Error("BOTL ERROR: " + ex);
			return false;
		}
	}

	public static void StaticInitializeLogging()
	{
		Repl.StandardError = (Repl.StandardOutput = (_logWriter = new DebugWriter()));
	}

	public void InitializeLogging()
	{
		islogging.Value.Set(b: false);
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		_logWriter?.TrySendToDebug();
	}
}
