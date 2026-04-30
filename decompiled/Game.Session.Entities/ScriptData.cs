using System.Collections.Generic;
using Game.Session.Actions;

namespace Game.Session.Entities;

public sealed class ScriptData : BaseData
{
	public List<GameScriptQueue.SavedScript> savedQueue;
}
