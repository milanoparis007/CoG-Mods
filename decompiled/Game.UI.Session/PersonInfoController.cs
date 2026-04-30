using Game.Core;
using Game.Session;
using Game.Session.Entities;

namespace Game.UI.Session;

public class PersonInfoController : HUDController<PersonInfoModel, PersonInfoDialog, PersonInfoController>
{
	public override void Initialize(PersonInfoDialog view)
	{
		base.Initialize(view);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandStarted, MaybeRefreshAIDebug);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandExecutedOneTurn, MaybeRefreshAIDebug);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandFinished, MaybeRefreshAIDebug);
	}

	public override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandStarted, MaybeRefreshAIDebug);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandExecutedOneTurn, MaybeRefreshAIDebug);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandFinished, MaybeRefreshAIDebug);
		base.Release();
	}

	public void SetModel(Entity entity, Entity selectOnClose = null)
	{
		base.Model.Reset();
		base.Model.Initialize(entity, selectOnClose);
		base.View.RefreshHeader();
		SetCurrentPanel(PanelType.Connections);
	}

	public void Close()
	{
		Entity selectOnClose = base.Model.selectOnClose;
		Game.ctx.hud.Hide(base.View);
		if (selectOnClose != null)
		{
			Game.ctx.selection.SetActive(selectOnClose);
		}
	}

	public void OnGoToClick()
	{
		PersonInfoUtil.TweenCameraToEntity(base.Model.entity.Id);
	}

	public void OnLevelupClick()
	{
		base.Model.entity.components.agent.ShowLevelupPopup();
	}

	public void SetCurrentPanel(PanelType panelType)
	{
		base.View.OnSetCurrentPanel(panelType);
		PersonInfoUtil.TweenCameraToEntity(base.Model.entity);
	}

	public void SwitchToPerson(Entity e)
	{
		if (base.View.IsShowing)
		{
			base.Model.Push(e);
			SetCurrentPanel(PanelType.Connections);
			base.View.RefreshHeader();
			base.View.SetBackButton(base.Model.history.Count > 0);
		}
	}

	public void ReturnToPrevious()
	{
		if (base.Model.HasHistory)
		{
			base.Model.Pop();
			SetCurrentPanel(PanelType.Connections);
			base.View.RefreshHeader();
			base.View.SetBackButton(base.Model.HasHistory);
		}
		else
		{
			_ = base.View.IsShowing;
		}
	}

	public void SetConnectionFilter(ConnFilter filter)
	{
		base.Model.connFilter = filter;
		base.View.RefreshPanel();
	}

	private void MaybeRefreshAIDebug(SessionEvent sev)
	{
		if (base.View.IsShowing && !(sev.eid != base.Model.entity.Id))
		{
			PlayerID pid = base.Model.entity.data.agent.pid;
			if (!(sev.pid != pid))
			{
				base.View.RefreshPanel();
			}
		}
	}
}
