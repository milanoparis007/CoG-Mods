using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Session;

public class DebugConsoleEntry
{
	private static readonly List<string> NONE = new List<string>();

	public string command;

	public List<string> first;

	public List<string> second;

	public Func<string[], string> callback;

	public DebugConsoleEntry(string command, Func<string[], string> callback)
		: this(command, (string[])null, (string[])null, callback)
	{
	}

	public DebugConsoleEntry(string command, string[] first, Func<string[], string> callback)
		: this(command, first, null, callback)
	{
	}

	public DebugConsoleEntry(string command, string first, Func<string[], string> callback)
		: this(command, new string[1] { first }, null, callback)
	{
	}

	public DebugConsoleEntry(string command, string first, string[] second, Func<string[], string> callback)
		: this(command, new string[1] { first }, second, callback)
	{
	}

	public DebugConsoleEntry(string command, string first, string second, Func<string[], string> callback)
		: this(command, new string[1] { first }, new string[1] { second }, callback)
	{
	}

	public DebugConsoleEntry(string command, string[] first, string[] second, Func<string[], string> callback)
	{
		this.command = command;
		this.first = ((first == null) ? NONE : new List<string>(first));
		this.second = ((second == null) ? NONE : new List<string>(second));
		this.callback = callback;
	}

	public static string InvalidParam(string explanation, string[] args, int keepNFirst, params string[] remainders)
	{
		string text = string.Join(" ", args);
		IEnumerable<string> values = args.Take(keepNFirst).ToList().Concat(remainders);
		string text2 = string.Join(" ", values);
		return explanation + ": " + text + "\nExpected: " + text2;
	}
}
