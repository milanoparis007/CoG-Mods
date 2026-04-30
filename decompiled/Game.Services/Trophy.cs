using System.Collections.Generic;
using Game.Core;
using Game.Session;
using Game.Session.Data;
using UnityEngine;

namespace Game.Services;

public sealed class Trophy
{
	public struct RenderInfo
	{
		public Label swapId;

		public List<Vector2> drawPos;
	}

	public Label id;

	public RenderInfo render;

	public VisitRequirementList visReqs;

	public VisitRequirementList reqs;

	public List<SessionEventType> updateEvents = new List<SessionEventType>();

	public List<Label> validThrones = new List<Label>();

	public string loctitle;

	public string locdesc;

	public string Image;
}
