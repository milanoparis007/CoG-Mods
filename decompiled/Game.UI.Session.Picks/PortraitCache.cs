using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class PortraitCache : IAnimatedSubManager<HUDManager>, ISubManager<HUDManager>
{
	public class CacheEntry
	{
		public PortraitInfo info;

		public Sprite sprite;
	}

	private Dictionary<string, CacheEntry> _cache;

	private Queue<Action> _updates;

	public void Initialize(HUDManager _)
	{
		_cache = new Dictionary<string, CacheEntry>();
		_updates = new Queue<Action>();
	}

	public void Release()
	{
		foreach (CacheEntry value in _cache.Values)
		{
			Game.serv.portraits.DestroyPortrait(value.sprite);
			value.sprite = null;
		}
		_cache.Clear();
		_cache = null;
		_updates.Clear();
		_updates = null;
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		int num = _updates?.Count ?? 0;
		if (num <= 0)
		{
			return;
		}
		int val = (Game.ctx.hud.personInfo.IsShowing ? 3 : 1000);
		int num2 = Math.Min(num, val);
		for (int i = 0; i < num2; i++)
		{
			if (_updates.Count > 0)
			{
				_updates.Dequeue()();
			}
		}
	}

	public Sprite GetSpriteFor(Entity peep)
	{
		string key = PersonToInfoKey(peep);
		CacheEntry cacheEntry = _cache.FindOrNull(key);
		if (cacheEntry == null)
		{
			CacheEntry cacheEntry2 = (_cache[key] = GenerateNew(peep));
			cacheEntry = cacheEntry2;
			peep.data.person.portrait = _cache[key].info;
		}
		return cacheEntry.sprite;
	}

	private CacheEntry GenerateNew(Entity peep)
	{
		PortraitInfo info = PickPieces(peep);
		Sprite sprite = Game.serv.portraits.GenerateBlankPortrait(info);
		_updates.Enqueue(PopulateCachedEntryWithPortrait(info, sprite));
		return new CacheEntry
		{
			info = info,
			sprite = sprite
		};
	}

	private static Action PopulateCachedEntryWithPortrait(PortraitInfo info, Sprite sprite)
	{
		return delegate
		{
			try
			{
				Game.serv.portraits.PopulateCompositePortrait(info, sprite);
			}
			catch (Exception ex)
			{
				Logger.Error("Invalid portrait pieces for " + info.hat + " / " + info.head + " / " + info.bust, ex.Message, ex.StackTrace);
			}
		};
	}

	private PortraitInfo PickPieces(Entity peep)
	{
		PersonData person = peep.data.person;
		int num = (int)((person.portrait == null) ? peep.components.ident.hash : 0);
		PlayerInfo player = peep.components.agent.GetPlayer();
		bool flag = player?.IsJustCop ?? false;
		bool isfed = player?.IsJustFed ?? false;
		Gender g = person.g;
		Skin s = person.s;
		int hathash = (flag ? ((int)Game.ctx.scenario.rngseed) : num);
		if (num == 0)
		{
			return person.portrait;
		}
		return Game.serv.portraits.GetPortraitPieces(num, hathash, g, s, flag, isfed, peep.data.person.Ethnicity);
	}

	private string PersonToInfoKey(Entity peep)
	{
		PersonData person = peep.data.person;
		string value = ((person.s == Skin.Dark) ? "d" : ((person.s == Skin.Medium) ? "m" : "l"));
		string value2 = ((person.g == Gender.F) ? "f" : "m");
		uint hash = peep.components.ident.hash;
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		stringBuilder.Append(value);
		stringBuilder.Append(value2);
		stringBuilder.Append(hash);
		return stringBuilder.ToStringAndReturnToPool();
	}
}
