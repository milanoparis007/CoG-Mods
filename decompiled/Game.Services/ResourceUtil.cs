using System.Collections;
using UnityEngine;

namespace Game.Services;

public static class ResourceUtil
{
	public static Hashtable LoadResourceAndParse(string resource, ResourceType type)
	{
		TextAsset textAsset = Resources.Load(resource, typeof(TextAsset)) as TextAsset;
		if (textAsset == null)
		{
			return null;
		}
		return FileUtil.ParseAsHashtable(textAsset.text, type);
	}

	public static T LoadResourceParseAndDeserialize<T>(string resource, ResourceType type)
	{
		return Game.serv.serializer.instance.Deserialize<T>(LoadResourceAndParse(resource, type));
	}

	public static GameObject LoadAndInstantiateGameObject(string resource, GameObject parent = null)
	{
		Object original = Resources.Load(resource);
		if (parent != null)
		{
			return Object.Instantiate(original, parent.transform) as GameObject;
		}
		return Object.Instantiate(original) as GameObject;
	}
}
