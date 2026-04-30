using System;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Quests;
using Game.UI.Session.HUD;

namespace Game.UI.Session.Quests;

internal class RequestListItem : ItemListDialog.IEntry, IComparable
{
	public QuestUUID uuid;

	public EntityID owner;

	public QuestRequest request;

	public RequestListItem(EntityID eid)
	{
		owner = eid;
		request = Game.ctx.quests.FindRequestedQuestUnsafe(eid);
		uuid = request.uuid;
	}

	public string GetDebug()
	{
		return "Request " + request.questid;
	}

	public string GetName()
	{
		return owner.FindEntity().data.person.FullName;
	}

	public string GetIcon()
	{
		return Loc.Get("ui.pipicon.quest");
	}

	public string GetDescription()
	{
		Entity building = BuildingUtil.FindBuildingForBizOwner(owner);
		return Loc.Get("ui.pip.kb-qreq-should-start") + ".\n" + Loc.Get("ui.reports.requests.desc.location", "location", BuildingUtil.FindBuildingName(building));
	}

	public bool ShowGoTo()
	{
		return owner.IsValid;
	}

	public void OnGoTo()
	{
		PersonInfoUtil.TweenCameraToEntity(owner);
	}

	public int CompareTo(object obj)
	{
		return 0;
	}
}
