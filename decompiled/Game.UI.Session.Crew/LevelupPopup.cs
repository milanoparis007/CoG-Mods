using System;
using System.Collections.Generic;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Crew;

public class LevelupPopup : BasePopup
{
	private const string CARD_TEMPLATE = "Templates/Levelup Card";

	private const string CONTAINER = "Panel/Scroll View List/Viewport/Content";

	private const string CLOSE = "Panel/Close Button";

	private const string TASK = "Panel/Task";

	private Entity _peep;

	private List<LevelupDescription> _rows;

	private Action<LevelupDescription> _callback;

	private const string CARD_NAME = "Name";

	private const string CARD_ICON = "Icon";

	private const string CARD_BUTTON = "Button";

	public override UIReference UIReference => UIElements.LevelupPopup;

	public LevelupPopup(Entity peep, List<LevelupDescription> rows, Action<LevelupDescription> callback)
	{
		_peep = peep;
		_rows = rows;
		_callback = callback;
	}

	protected override void InitializeOnPush()
	{
		_go.GetButton("Panel/Close Button").onClick.SetListener(Close);
		string fullName = _peep.data.person.FullName;
		_go.GetText("Panel/Task").text = Loc.Get("ui.personinfo.levelups.header", "name", fullName);
		GameObject child = _go.GetChild("Templates/Levelup Card");
		GameObject child2 = _go.GetChild("Panel/Scroll View List/Viewport/Content");
		child2.EnsureChildCount(_rows, child);
		child2.InitializeChildren(_rows, MakeCrewCard);
	}

	protected override void ReleaseOnPop()
	{
		_callback = null;
		_peep = null;
		_rows = null;
		_go.GetChild("Panel/Scroll View List/Viewport/Content").DestroyAllChildren();
		_go.ClearButtonListeners("Panel/Close Button");
	}

	private void MakeCrewCard(int i, GameObject card, LevelupDescription desc)
	{
		card.SetText("Name", desc.message);
		card.SetTextOrHide("Icon", desc.icon);
		card.GetButton("Button").onClick.SetListener(delegate
		{
			Action<LevelupDescription> callback = _callback;
			Close();
			callback(desc);
		});
	}
}
