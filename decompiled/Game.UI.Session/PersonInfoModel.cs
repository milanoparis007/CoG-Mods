using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.UI.Session;

public class PersonInfoModel : HUDModel<PersonInfoModel, PersonInfoDialog, PersonInfoController>
{
	public Entity entity;

	public PersonInfoUtil.Overview data;

	public List<EntityID> history = new List<EntityID>();

	public Entity selectOnClose;

	public ConnFilter connFilter;

	public bool HasHistory => history.Count > 1;

	public EntityID EntityID => entity?.Id ?? EntityID.INVALID;

	public override void Reset()
	{
		base.Reset();
		history.Clear();
		entity = null;
		data = default(PersonInfoUtil.Overview);
		selectOnClose = null;
	}

	public void Initialize(Entity entity, Entity selectOnClose)
	{
		this.selectOnClose = selectOnClose;
		Push(entity);
	}

	public void Push(Entity entity)
	{
		this.entity = entity;
		data = PersonInfoUtil.GenerateOverview(entity, details: true);
		history.Add(entity.Id);
	}

	public void Pop()
	{
		if (history.Count > 1)
		{
			history.RemoveLastOrDefault();
			entity = history.LastOrDefaultFast().FindEntity();
			data = PersonInfoUtil.GenerateOverview(entity, details: true);
		}
	}
}
