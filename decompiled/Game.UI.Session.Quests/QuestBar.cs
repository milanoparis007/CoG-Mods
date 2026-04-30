using System;
using System.Collections.Generic;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Quests;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Quests;

public sealed class QuestBar : BaseHUDDialog
{
	private class QuestBarMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			int questCount = Game.ctx.hud.quests.GetQuestCount();
			int activeQuestCap = Game.ctx.quests.GetActiveQuestCap();
			string text = Game.ctx.quests.ExplainActiveQuestCap();
			if (questCount > activeQuestCap)
			{
				string text2 = AbstractModifier.FormatDelta(questCount - activeQuestCap);
				text = text + "\n" + ModifierList.IndentExplanation(Loc.Get("ui.questbar.mo.extras", "delta", text2));
			}
			return Loc.Get("ui.questbar.mo", "num", questCount, "max", activeQuestCap, "explanation", text);
		}
	}

	private GameObject _templates;

	private GameObject _panel;

	private GameObject _cardContainer;

	private GameObject _tmplButton;

	private List<QuestBarButton> _quests;

	private const string ALL_TEMPLATES = "Templates";

	private const string TMPL_QUEST_BUTTON = "Templates/Quest Button";

	private const string PANEL = "Panel";

	private const string CONTAINER = "Viewport/Content/Container";

	private const string HEADER_TEXT = "Header/Text";

	public override bool ShowAtStartup => true;

	public override TweenType Tween => TweenType.None;

	public override UIReference UIReference => UIElements.QuestBar;

	internal override void Initialize()
	{
		base.Initialize();
		_templates = _go.GetChild("Templates");
		_templates.SetActive(value: false);
		_tmplButton = _go.GetChild("Templates/Quest Button");
		_cardContainer = _go.GetChild("Viewport/Content/Container");
		_panel = _go.GetChild("Panel");
		_panel.SetActive(value: false);
		_quests = new List<QuestBarButton>();
		Game.ctx.events.AddListener(SessionEventType.QuestProgressImmediate, OnQuestProgress);
		Game.ctx.events.AddListener(SessionEventType.QuestCompletedImmediate, OnQuestCompleted);
		Game.ctx.events.AddListener(SessionEventType.QuestCanceledImmediate, OnQuestCanceled);
		Game.serv.mouseovers.Register(MouseoverType.QuestBar, new QuestBarMouseover());
	}

	internal override void Release()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.QuestBar);
		Game.ctx.events.RemoveListener(SessionEventType.QuestProgressImmediate, OnQuestProgress);
		Game.ctx.events.RemoveListener(SessionEventType.QuestCompletedImmediate, OnQuestCompleted);
		Game.ctx.events.RemoveListener(SessionEventType.QuestCanceledImmediate, OnQuestCanceled);
		RemoveAll();
		_templates = (_panel = (_cardContainer = (_tmplButton = null)));
		_quests = null;
		base.Release();
	}

	public List<QuestUUID> GetQuestsSorted()
	{
		return _quests.SelectIntoNewList((QuestBarButton b) => b.uuid);
	}

	public int GetQuestCount()
	{
		return _quests.Count;
	}

	public void Add(QuestUUID uuid)
	{
		if (!uuid.IsNotSet)
		{
			QuestBarButton questBarButton = new QuestBarButton();
			GameObject card = UnityEngine.Object.Instantiate(_tmplButton, _cardContainer.transform);
			questBarButton.Initialize(card, uuid);
			_quests.Add(questBarButton);
			_quests.Sort(delegate(QuestBarButton quest, QuestBarButton other)
			{
				QuestActiveRecord questActiveRecord = Game.ctx.quests.FindActiveQuestUnsafe(quest.uuid);
				QuestActiveRecord questActiveRecord2 = Game.ctx.quests.FindActiveQuestUnsafe(other.uuid);
				return (questActiveRecord != null && questActiveRecord2 != null) ? questActiveRecord.CompareTo(questActiveRecord2) : 0;
			});
			for (int num = 0; num < _quests.Count; num++)
			{
				_quests[num].card.transform.SetSiblingIndex(num);
			}
			UpdateHeaderAndVisibility();
		}
	}

	private void UpdateHeaderAndVisibility()
	{
		int num = Math.Max(_quests.Count, Game.ctx.quests.GetActiveQuestCap());
		_panel.SetActive(_quests.Count > 0);
		_go.SetText("Header/Text", Loc.Get("ui.questbar.header", "num", _quests.Count, "max", num));
	}

	private void OnQuestRemoveButton(QuestBarButton button)
	{
		if (button != null)
		{
			Remove(button.uuid);
		}
	}

	public void Remove(QuestUUID uuid)
	{
		QuestBarButton questBarButton = _quests.Find((QuestBarButton b) => b.uuid == uuid);
		if (questBarButton != null)
		{
			UnityEngine.Object.Destroy(questBarButton.card);
			questBarButton.Release();
			_quests.Remove(questBarButton);
			UpdateHeaderAndVisibility();
		}
	}

	public void RemoveAll()
	{
		while (_quests.Count > 0)
		{
			Remove(_quests[0].uuid);
		}
	}

	private QuestBarButton FindOrNull(QuestUUID uuid)
	{
		return _quests.Find((QuestBarButton button) => button.uuid.Equals(uuid));
	}

	private QuestBarButton FindOrNull(SessionEvent ev)
	{
		if (!(ev.ctx is QuestUUID uuid))
		{
			return null;
		}
		return FindOrNull(uuid);
	}

	private void OnQuestProgress(SessionEvent ev)
	{
		FindOrNull(ev)?.RefreshContents();
	}

	private void OnQuestCompleted(SessionEvent ev)
	{
		OnQuestRemoveButton(FindOrNull(ev));
	}

	private void OnQuestCanceled(SessionEvent ev)
	{
		OnQuestRemoveButton(FindOrNull(ev));
	}
}
