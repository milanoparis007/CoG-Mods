using System;
using Discord;
using UnityEngine;

namespace Game.Services;

public class DiscordHandlerImpl : IDiscordHandler
{
	private global::Discord.Discord _discord;

	private DateTime _nextUpdate;

	public bool IsUsingDiscord
	{
		get
		{
			if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.OSXPlayer)
			{
				return Application.platform == RuntimePlatform.OSXEditor;
			}
			return true;
		}
	}

	public virtual void Connect()
	{
		if (!IsUsingDiscord)
		{
			return;
		}
		ValidateDiscordFailure();
		try
		{
			_discord = new global::Discord.Discord(DiscordService.DISCORD_CLIENTID, 1uL);
			_discord.SetLogHook(LogLevel.Debug, delegate
			{
			});
			_discord.GetApplicationManager();
		}
		catch (ResultException ex)
		{
			if (ex.Result != Result.InternalError)
			{
				Logger.Error("Discord result error:", ex);
			}
			_discord = null;
		}
		catch (Exception ex2)
		{
			Logger.Error("Discord error:", ex2);
			_discord = null;
		}
		static void ValidateDiscordFailure()
		{
			if (new ResultException(Result.InternalError).Result != Result.InternalError)
			{
				Logger.Error("Discord Game SDK is still borked, please fix ResultException in Core.cs manually");
			}
		}
	}

	public virtual void Disconnect()
	{
		_discord?.Dispose();
		_discord = null;
	}

	public virtual void Post(string state, string details, string imgname, string imgtext)
	{
		if (_discord != null)
		{
			Activity activity = new Activity
			{
				State = Encode(state),
				Details = Encode(details),
				Assets = 
				{
					LargeImage = imgname,
					LargeText = Encode(imgtext)
				}
			};
			_discord.GetActivityManager().UpdateActivity(activity, delegate
			{
			});
		}
	}

	public virtual void Clear()
	{
		if (_discord != null)
		{
			_discord.GetActivityManager().UpdateActivity(default(Activity), delegate
			{
			});
		}
	}

	public string Encode(string input)
	{
		return input;
	}

	public virtual void Update()
	{
		DateTime now = DateTime.Now;
		if (now < _nextUpdate)
		{
			return;
		}
		_nextUpdate = now.AddSeconds(5.0);
		try
		{
			_discord?.RunCallbacks();
		}
		catch (ResultException ex)
		{
			if (ex.Result != Result.NotRunning && ex.Result != Result.NotInstalled)
			{
				Logger.Error("Discord result error:", ex.Result, ex);
			}
		}
		catch (Exception ex2)
		{
			Logger.Error("Discord error:", ex2);
		}
	}
}
