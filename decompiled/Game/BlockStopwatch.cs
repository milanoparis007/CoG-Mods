using System;
using System.Diagnostics;

namespace Game;

public class BlockStopwatch : IDisposable
{
	public Stopwatch stopwatch;

	public string name;

	public string devkey;

	public BlockStopwatch(string name)
	{
	}

	public BlockStopwatch(string devkey, string name)
	{
	}

	public void Dispose()
	{
	}
}
