using System;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.UI.Session;

public abstract class EntityInfoDialog : BaseHUDDialog
{
	protected Entity _entity;

	protected CrewAssignment _crew;

	public Entity Entity => _entity;

	public virtual void Show(Entity entity, CrewAssignment crewNearby)
	{
		_entity = entity;
		_crew = crewNearby;
		base.Show();
	}

	public override void Show()
	{
		throw new NotSupportedException("Use Show(entity) on EntityInfoDialog!");
	}

	public virtual void Toggle(Entity entity)
	{
		Toggle(entity, CrewAssignment.EMPTY, !base.IsShowing);
	}

	public virtual void Toggle(Entity entity, bool show)
	{
		Toggle(entity, CrewAssignment.EMPTY, show);
	}

	public virtual void Toggle(Entity entity, CrewAssignment crew)
	{
		Toggle(entity, crew, !base.IsShowing);
	}

	public virtual void Toggle(Entity entity, CrewAssignment crew, bool show)
	{
		if (show && !base.IsShowing)
		{
			Show(entity, crew);
		}
		if (!show && base.IsShowing)
		{
			Hide();
		}
	}

	protected override void OnAfterHide()
	{
		_entity = null;
		base.OnAfterHide();
	}
}
