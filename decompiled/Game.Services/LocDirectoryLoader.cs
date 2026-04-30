using System;
using System.Collections.Generic;
using System.Linq;
using SomaSim.Util;

namespace Game.Services;

internal sealed class LocDirectoryLoader : DirectoryLoader
{
	private Action<List<string>> _onLoaded;

	public LocDirectoryLoader(string directory, Action<List<string>> onLoadedCallback)
		: base(directory, ResourceType.TextFile)
	{
		_onLoaded = onLoadedCallback;
		base.OnDone += OnAllTextLoaded;
	}

	private void OnAllTextLoaded()
	{
		base.OnDone -= OnAllTextLoaded;
		base.Files.ForEach(delegate
		{
		});
		List<string> obj = new List<string>(base.Files.Select((KeyValuePair<string, FileContents> entry) => entry.Value?.text));
		_onLoaded(obj);
	}
}
