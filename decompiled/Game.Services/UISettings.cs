using System.Collections.Generic;
using Game.Core;
using Game.Session;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Services;

public sealed class UISettings : IValidatingSettings
{
	public UIPhotosSettings photos;

	public NewspaperConfig newspaperDefaults;

	public LabelDictionary<ConvoStateDef> convos;

	public LabelDictionary<UIQuery> playerQueries;

	public List<ModuleIntro> moduleIntros;

	private Dictionary<SessionEventType, List<Label>> _queriesByResetEventCache;

	public void Validate()
	{
		convos.Values.ForEach(delegate(ConvoStateDef def)
		{
			ValidateConvo(def);
		});
	}

	private void ValidateConvo(ConvoStateDef def)
	{
		def.buttons?.ForEach(delegate(ConvoButtonDef button)
		{
			ValidateConvoButton(def, button);
		});
	}

	private void ValidateConvoButton(ConvoStateDef _, ConvoButtonDef button)
	{
		button.next?.Verify(button);
		ValidateCallbackExists("onClick", button.onClick);
		ValidateCallbackExists("onShow", button.onShow);
		ValidateCallbackExists("onPreshow", button.onPreshow);
	}

	private void ValidateCallbackExists(string type, string name)
	{
		if (name != null && !ConvoCallbacks.AllCallbackNames.Contains(name))
		{
			Logger.Error(string.Concat("Unknown " + type + " callback: " + name + ". ", " Convo callbacks must be public/internal and accept a ConvoButton argument."));
		}
	}

	internal UIQuery FindQueryOrNull(Label qid)
	{
		return playerQueries.FindOrNull(qid);
	}

	internal bool IsValidQueryID(Label qid)
	{
		return playerQueries.ContainsKey(qid);
	}

	internal List<Label> FindQueriesResetByEvent(SessionEventType t)
	{
		PopulateQueriesCache();
		return _queriesByResetEventCache.FindOrMakeList(t);
	}

	internal IEnumerable<SessionEventType> FindEventsThatResetQueries()
	{
		PopulateQueriesCache();
		return _queriesByResetEventCache.Keys;
	}

	private void PopulateQueriesCache()
	{
		if (_queriesByResetEventCache != null)
		{
			return;
		}
		_queriesByResetEventCache = new Dictionary<SessionEventType, List<Label>>(new SessionEventTypeComparer());
		foreach (KeyValuePair<Label, UIQuery> playerQuery in playerQueries)
		{
			if (playerQuery.Value.resetevents == null)
			{
				continue;
			}
			foreach (SessionEventType resetevent in playerQuery.Value.resetevents)
			{
				_queriesByResetEventCache.AddToList(resetevent, playerQuery.Key);
			}
		}
	}
}
