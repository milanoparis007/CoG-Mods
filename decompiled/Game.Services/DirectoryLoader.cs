using System;
using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Services;

public class DirectoryLoader
{
	public class FileContents
	{
		public string text;
	}

	protected ResourceType _type;

	public Dictionary<string, FileContents> Files { get; private set; }

	public bool IsDone => Files == null;

	public event Action OnDone = delegate
	{
	};

	public DirectoryLoader(string directory, ResourceType type)
	{
		_type = type;
		Files = new Dictionary<string, FileContents>();
		foreach (string streamingAssetFileName in Game.platform.GetStreamingAssetFileNames(directory, type))
		{
			Files.Add(streamingAssetFileName, new FileContents());
		}
	}

	public void Start()
	{
		foreach (string key in Files.Keys)
		{
			Load(key);
		}
	}

	public void Stop()
	{
		Files = null;
	}

	private void Load(string file)
	{
		StreamingAssetsUtil.LoadAsString(file, _type, delegate(string contents)
		{
			if (!IsDone)
			{
				Files[file].text = (string.IsNullOrEmpty(contents) ? "" : contents);
				TestAllLoaded();
			}
		});
	}

	private void TestAllLoaded()
	{
		if (!IsDone && !Files.Contains((KeyValuePair<string, FileContents> entry) => entry.Value.text == null))
		{
			this.OnDone();
			Stop();
		}
	}
}
