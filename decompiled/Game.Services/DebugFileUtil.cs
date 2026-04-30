using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Services;

public static class DebugFileUtil
{
	[Conditional("UNITY_EDITOR")]
	public static void ProduceDebugTextFile(string type, Dictionary<string, int> counts, string header)
	{
		List<string> list = counts.Keys.ToList();
		list.Sort();
		StringBuilder stringBuilder = new StringBuilder(16384);
		stringBuilder.AppendLine(header);
		foreach (string item in list)
		{
			stringBuilder.AppendLine($"{item},{counts[item]}");
		}
		string text = stringBuilder.ToString();
		ProduceDebugTextFile(type, text);
	}

	public static string ProduceDebugTextFile(string type, string text)
	{
		string text2 = Path.Combine(Application.temporaryCachePath, $"log-{type}-{DateTime.Now.Ticks}.txt");
		File.WriteAllText(text2, text);
		Logger.LogAlways("Wrote log file: " + text2);
		Process.Start("notepad.exe", text2);
		return text2;
	}

	[Conditional("UNITY_EDITOR")]
	public static void ProduceStatsCsvFile(string text)
	{
	}
}
