using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI.Session;

public sealed class DebugConsoleDialog : BaseHUDDialog, IKeyboardHandler
{
	private class BugReportKeyboardHandler : IKeyboardHandler
	{
		public KeyboardHandler GetKeyHandler()
		{
			return new CapturingKeyboardHandler(() => true, KeyboardHandler.Priority.HighestModalDialog);
		}
	}

	private TMP_InputField _input;

	private TextMeshProUGUI _output;

	private const int MAX_HISTORY_SIZE = 100;

	private List<string> _history = new List<string> { "" };

	private int _historyIndex;

	private KeyboardHandler _keyhandler;

	private List<string> NONE = new List<string>();

	private BugReportKeyboardHandler _bugReportKeyboardHandler = new BugReportKeyboardHandler();

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Up;

	public override UIReference UIReference => UIElements.DebugConsole;

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		_input = _go.GetChild<TMP_InputField>("Input");
		_output = _go.GetChild<TextMeshProUGUI>("Output");
		_input.onSubmit.AddListener(OnSubmit);
		_input.resetOnDeActivation = true;
		_input.restoreOriginalTextOnEscape = false;
		_keyhandler = new CapturingKeyboardHandler(() => _input.isFocused, KeyboardHandler.Priority.HighNonModalDialog);
		_keyhandler.keyhandlers.Add(new KeyInput(KeyCode.UpArrow, OnUpArrow));
		_keyhandler.keyhandlers.Add(new KeyInput(KeyCode.DownArrow, OnDownArrow));
		_keyhandler.keyhandlers.Add(new KeyInput(KeyCode.Tab, OnTab));
	}

	protected override void OnAfterShow()
	{
		base.OnAfterShow();
		Game.serv.keyboard.PushHandler(this);
		_input.ActivateInputField();
	}

	protected override void OnBeforeHide()
	{
		Game.serv.keyboard.RemoveHandler(this);
		base.OnBeforeHide();
	}

	protected override void OnAfterHide()
	{
		_keyhandler.keyhandlers.Clear();
		_keyhandler = null;
		_input.onSubmit.RemoveAllListeners();
		_input = null;
		_output = null;
		base.OnAfterHide();
	}

	public KeyboardHandler GetKeyHandler()
	{
		return _keyhandler;
	}

	private string MoveCursorAndGetHistory(int delta)
	{
		_historyIndex = MathUtil.Clamp(_historyIndex + delta, 0, _history.Count);
		if (_historyIndex >= _history.Count)
		{
			return "";
		}
		return _history[_historyIndex];
	}

	private void AddToHistory(string text)
	{
		if (_history.LastOrDefaultFast() != text)
		{
			_history.Add(text);
		}
		if (_history.Count > 100)
		{
			_history.RemoveAt(0);
		}
		_historyIndex = _history.Count;
	}

	private void OnUpArrow()
	{
		_input.text = MoveCursorAndGetHistory(-1);
		_input.MoveToEndOfLine(shift: false, ctrl: false);
	}

	private void OnDownArrow()
	{
		_input.text = MoveCursorAndGetHistory(1);
		_input.MoveToEndOfLine(shift: false, ctrl: false);
	}

	private void OnTab()
	{
		string text = _input.text;
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		List<string> list = new List<string>();
		string[] array = text.Split(' ');
		if (array.Length != 0)
		{
			foreach (DebugConsoleEntry allEntry in Game.ctx.console.GetAllEntries())
			{
				list.AddRange(OfferSuggestions(allEntry, array));
			}
		}
		list = (from s in list.Distinct()
			orderby s
			select s).ToList();
		if (list.Count <= 1)
		{
			_output.SetText("");
		}
		else if (list.Count <= 15)
		{
			_output.SetText("Some possible completions:\n" + string.Join("\n", list));
		}
		else
		{
			_output.SetText($"Some possible completions (showing {15} of {list.Count}):\n" + string.Join("\n", list.Take(15).ToList()) + $"\n(... and {list.Count - 15} more)");
		}
		string text2 = ((list.Count > 0) ? list.Aggregate(LongestSharedStart) : text);
		_input.text = text2;
		_input.MoveToEndOfLine(shift: false, ctrl: false);
	}

	private static string LongestSharedStart(string a, string b)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		int num = Math.Min(a.Length, b.Length);
		for (int i = 0; i < num && a[i] == b[i]; i++)
		{
			stringBuilder.Append(a[i]);
		}
		return stringBuilder.ToStringAndReset();
	}

	private void OnSubmit(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			Game.ctx.hud.console.Hide();
			return;
		}
		AddToHistory(text);
		_output.text = ProcessInput(text);
		_input.text = "";
		_input.ActivateInputField();
	}

	private string ProcessInput(string text)
	{
		string[] array = text.TrimEnd().Split(' ');
		if (array.Length != 0)
		{
			foreach (DebugConsoleEntry allEntry in Game.ctx.console.GetAllEntries())
			{
				string text2 = ProcessEntry(allEntry, array);
				if (text2 != null)
				{
					return text2;
				}
			}
		}
		string text3 = Game.ctx.players?.Human?.social?.GetPlayerPeep()?.data?.person?.FirstName;
		return "I'm sorry " + text3 + ", I'm afraid I can't do that.";
	}

	private string ProcessEntry(DebugConsoleEntry entry, string[] segments)
	{
		if (entry.command != segments[0])
		{
			return null;
		}
		if (segments.Length > 1 && entry.first.Count > 0 && !entry.first.Contains(segments[1]))
		{
			return null;
		}
		if (segments.Length > 2 && entry.second.Count > 0 && !entry.second.Contains(segments[2]))
		{
			return null;
		}
		Game.ctx.events.SendImmediate(SessionEventType.Cheated);
		return entry.callback(segments);
	}

	private List<string> OfferSuggestions(DebugConsoleEntry entry, string[] segments)
	{
		if (segments.Length == 0)
		{
			return NONE;
		}
		if (segments.Length == 1)
		{
			if (!entry.command.StartsWith(segments[0], StringComparison.OrdinalIgnoreCase))
			{
				return NONE;
			}
			return new List<string> { entry.command };
		}
		if (!entry.command.Equals(segments[0], StringComparison.OrdinalIgnoreCase))
		{
			return NONE;
		}
		if (segments.Length == 2)
		{
			return (from first in entry.first
				where first.StartsWith(segments[1], StringComparison.OrdinalIgnoreCase)
				select entry.command + " " + first).ToList();
		}
		List<string> list = new List<string>();
		foreach (string item in entry.first)
		{
			if (!entry.first.Contains(segments[1], StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}
			foreach (string item2 in entry.second)
			{
				if (item2.StartsWith(segments[2], StringComparison.OrdinalIgnoreCase))
				{
					list.Add(entry.command + " " + item + " " + item2);
				}
			}
		}
		return list;
	}

	public void StartBugReport()
	{
		Game.serv.keyboard.PushHandler(_bugReportKeyboardHandler);
		string descriptionContent = "Please enter description here...\n\n\n--------------------------------\n" + $"Scenario {Game.ctx.session.mapconfig.id}, random value: {Game.ctx.scenario.rngseed}\n" + $"Game version {GameSettings.version}, build {GameSettings.build}, uid {Game.serv.saveload.progress.stats.uuid}";
		SRDebug.Instance.ShowBugReportSheet(EndBugReport, takeScreenshot: true, descriptionContent);
	}

	public void EndBugReport(bool success = false)
	{
		Game.serv.keyboard.RemoveHandler(_bugReportKeyboardHandler);
	}
}
