using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Services.Filesystem;
using SomaSim.SION;
using SomaSim.Util;
using UnityEngine.Networking;

namespace Game.Services;

public class RemoteSettingsService : AbstractService
{
	public enum State
	{
		NotStarted,
		Loading,
		Success,
		Failure
	}

	public Listeners OnInitializationFinished;

	public State state { get; private set; }

	public RemoteSettingsDefinition settings { get; private set; }

	public RemoteSettingsService()
	{
		state = State.NotStarted;
		settings = new RemoteSettingsDefinition();
	}

	public override void OnInitialized()
	{
		OnInitializationFinished = new Listeners();
	}

	public override void OnReleased()
	{
		OnInitializationFinished.Clear();
	}

	public void Start()
	{
		if (state == State.NotStarted)
		{
			bool flag = true;
			if (Game.settings.IsDesktop && flag)
			{
				Game.instance.StartCoroutine(DoStartDownload());
			}
			else
			{
				MarkFailed();
			}
		}
	}

	private void MarkFailed(Exception ex = null)
	{
		if (ex != null)
		{
			Logger.LogAlways("Remote settings exception: " + ex.Message);
		}
		state = State.Failure;
	}

	private IEnumerator DoStartDownload()
	{
		state = State.Loading;
		string uri = "https://storage.googleapis.com/cog-static/cogs.txt?t=" + DateTime.Now.Millisecond;
		using UnityWebRequest www = UnityWebRequest.Get(uri);
		yield return www.SendWebRequest();
		if (www != null && www.result == UnityWebRequest.Result.Success)
		{
			string text = www.downloadHandler.text;
			Process(text);
		}
		else
		{
			MarkFailed();
		}
	}

	private void Process(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			MarkFailed();
			return;
		}
		if (!text.StartsWith("{"))
		{
			MarkFailed();
			return;
		}
		try
		{
			Hashtable hashtable = FileUtil.ParseAsHashtable(text, ResourceType.SimFile);
			if (hashtable == null)
			{
				MarkFailed();
				return;
			}
			Serializer serializer = Game.serv.serializer.CloneInstance();
			settings = serializer.Deserialize<RemoteSettingsDefinition>(hashtable);
			state = State.Success;
			OnInitializationFinished.Invoke();
		}
		catch (Exception ex)
		{
			MarkFailed(ex);
		}
	}

	public RemoteSettingsDefinition.Announcement FindRandomAnnouncementOrNull()
	{
		if (state != State.Success)
		{
			return null;
		}
		List<RemoteSettingsDefinition.Announcement> announcements = settings.announcements;
		if (announcements == null || announcements.Count == 0)
		{
			return null;
		}
		GameplayProgressFile prefs = Game.serv.saveload.progress;
		if (prefs?.announcements == null)
		{
			return null;
		}
		List<RemoteSettingsDefinition.Announcement> list = announcements.Where((RemoteSettingsDefinition.Announcement def) => Validate(def, prefs)).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return new SplitMix64((uint)DateTime.Now.Ticks).PickElement(list);
	}

	private static bool Validate(RemoteSettingsDefinition.Announcement def, GameplayProgressFile prefs)
	{
		return ValidateHelper(def, prefs).ok;
	}

	private static (bool ok, string msg) ValidateHelper(RemoteSettingsDefinition.Announcement def, GameplayProgressFile prefs)
	{
		if (def == null)
		{
			return (ok: false, msg: "Missing def");
		}
		if (prefs.announcements.ContainsKey(def.id))
		{
			return (ok: false, msg: "Announcement hidden: " + def.id);
		}
		int session = prefs.stats.session;
		if (session < def.min)
		{
			return (ok: false, msg: $"Session {session} below {def.min}");
		}
		int num = session - def.min;
		if (def.mod > 1 && MathUtil.Modulus(num, def.mod) != 0)
		{
			return (ok: false, msg: $"Mod session {num} failed mod {def.mod}");
		}
		string currentLang = Game.serv.loc.currentLang;
		if (def.langs != null && !def.langs.Contains(currentLang))
		{
			return (ok: false, msg: "Languange " + currentLang + " not valid for this message");
		}
		return (ok: true, msg: "All tests are go");
	}

	public void HideAnnouncement(string id)
	{
		Dictionary<string, bool> announcements = Game.serv.saveload.progress.announcements;
		if (announcements != null)
		{
			announcements[id] = true;
			Game.serv.saveload.SaveProgress();
		}
	}
}
