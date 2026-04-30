using System;
using System.Collections;
using System.Collections.Generic;
using SomaSim.SION;

namespace Game.Services;

internal class SettingsDirectoryLoader : DirectoryLoader
{
	private bool _partialArrays;

	private Action<Hashtable> _onLoaded;

	public SettingsDirectoryLoader(string directory, bool arrayFiles, Action<Hashtable> onLoadedCallback)
		: base(directory, ResourceType.SimFile)
	{
		_partialArrays = arrayFiles;
		_onLoaded = onLoadedCallback;
		base.OnDone += OnAllTextLoaded;
	}

	private void OnAllTextLoaded()
	{
		base.OnDone -= OnAllTextLoaded;
		bool partialArrays = _partialArrays;
		Hashtable hashtable = new Hashtable();
		foreach (KeyValuePair<string, FileContents> file in base.Files)
		{
			try
			{
				string text = file.Value?.text;
				Hashtable child = (string.IsNullOrEmpty(text) ? new Hashtable() : FileUtil.ParseAsHashtable(text, _type));
				hashtable = HashtableMerger.Merge(hashtable, child, partialArrays) as Hashtable;
			}
			catch (Exception ex)
			{
				Logger.Error("Error parsing file " + file.Key + ": " + ex.Message + "\n" + ex.StackTrace);
				throw ex;
			}
		}
		try
		{
			_onLoaded(hashtable);
		}
		catch (Exception ex2)
		{
			Logger.Error("Error deserializing merged settings files: " + ex2.Message + " \n" + ex2.StackTrace);
			throw ex2;
		}
	}
}
