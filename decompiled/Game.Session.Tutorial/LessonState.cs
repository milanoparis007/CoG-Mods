using System;
using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Session.Tutorial;

public class LessonState
{
	public List<BaseLesson> lessons = new List<BaseLesson>();

	public BaseLesson active;

	public CoroutineTask task;

	public DateTime lastUpdate;

	public int lastFinished;

	public bool IsActive => active != null;

	public bool CanActivate
	{
		get
		{
			if (active == null)
			{
				return lessons.Count > 0;
			}
			return false;
		}
	}

	public TimeSpan TimeSinceLastUpdate => DateTime.Now - lastUpdate;

	public BaseLesson StartNextLesson()
	{
		active = lessons.RemoveAndReturn(0);
		task = Game.serv.sequencer.StartCoroutineTask(active.Run(), OnFinished);
		lastUpdate = DateTime.Now;
		Game.serv.stats.LogEvent("tutorial", $"start_{active.LessonNumber}");
		return active;
	}

	public void StopActiveLesson()
	{
		if (task != null)
		{
			task.Stop();
		}
	}

	private void OnFinished(CoroutineTask task)
	{
		Game.serv.stats.LogEvent("tutorial", $"end_{active.LessonNumber}");
		Game.ctx.tutorial.HideFrancineDialog();
		Game.ctx.tutorial.ClearHighlights();
		this.task = null;
		lastFinished = active.LessonNumber;
		active = null;
		lastUpdate = DateTime.Now;
	}
}
