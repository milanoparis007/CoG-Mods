using System;
using System.IO;
using UnityEngine;

namespace Game.Services.Mods;

public class DefaultFilesystemProvider
{
	protected readonly string _subdirname;

	protected string _subdirpath;

	protected DirectoryInfo _subdir;

	public bool enabled => _subdir != null;

	public DefaultFilesystemProvider(string subdirectory)
	{
		_subdirname = subdirectory;
	}

	public virtual void Initialize()
	{
		try
		{
			_subdirpath = Path.Combine(Application.persistentDataPath, _subdirname);
			_subdir = new DirectoryInfo(_subdirpath);
			if (!_subdir.Exists)
			{
				_subdir.Create();
			}
		}
		catch (Exception ex)
		{
			_subdirpath = null;
			_subdir = null;
			Logger.Warning("Unity persistent data path error: " + ex);
		}
	}

	public virtual void Release()
	{
		_subdir = null;
		_subdirpath = null;
	}

	public string MakePath(string basename, string suffix)
	{
		return Path.Combine(_subdirpath, basename + suffix);
	}
}
