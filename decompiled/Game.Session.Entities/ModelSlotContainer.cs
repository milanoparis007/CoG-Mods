using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public sealed class ModelSlotContainer
{
	public List<ModelSlot> core;

	public List<ModelSlot> facade;

	public List<ModelSlot> decofront;

	public List<ModelSlot> decoback;

	public List<ModelSlot> extra;

	public void DoRoutine(Action<List<ModelSlot>, bool> routine)
	{
		routine(core, arg2: true);
		routine(facade, arg2: false);
		routine(decofront, arg2: false);
		routine(decoback, arg2: false);
		routine(extra, arg2: false);
	}
}
