using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.U2D;

namespace Game.UI.Session;

public sealed class SpriteAtlasCache
{
	private string _atlasName;

	private Dictionary<string, Sprite> _sprites;

	public SpriteAtlasCache(string atlasName)
	{
		_atlasName = atlasName;
		_sprites = new Dictionary<string, Sprite>();
		SpriteAtlas obj = Resources.Load(atlasName) as SpriteAtlas;
		Sprite[] array = new Sprite[obj.spriteCount];
		obj.GetSprites(array);
		Sprite[] array2 = array;
		foreach (Sprite sprite in array2)
		{
			string key = sprite.name.Replace("(Clone)", "");
			_sprites[key] = sprite;
		}
	}

	public Sprite Find(string name)
	{
		if (name == null)
		{
			return null;
		}
		Sprite sprite = _sprites.FindOrNull(name);
		_ = sprite == null;
		return sprite;
	}

	public void Reset()
	{
		foreach (Sprite value in _sprites.Values)
		{
			Object.Destroy(value);
		}
		_sprites.Clear();
	}
}
