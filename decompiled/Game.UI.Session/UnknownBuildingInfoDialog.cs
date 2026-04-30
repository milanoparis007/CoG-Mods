using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Entities;
using Game.UI.Mouseovers;
using UnityEngine;

namespace Game.UI.Session;

public sealed class UnknownBuildingInfoDialog : EntityInfoDialog
{
	public sealed class UnknownBuildingMouseover : BaseCustomTextMouseover
	{
		private UnknownBuildingInfoDialog dialog;

		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

		private Entity Entity => dialog?._entity;

		public UnknownBuildingMouseover(UnknownBuildingInfoDialog dialog)
		{
			this.dialog = dialog;
		}

		protected override string ProduceText()
		{
			bool flag = Entity.components.board.IsKnown(PlayerID.HumanPlayer);
			bool isResidenceBuildingType = Entity.components.building.IsResidenceBuildingType;
			bool isCivicBuildingType = Entity.components.building.IsCivicBuildingType;
			bool flag2 = !isResidenceBuildingType && !isCivicBuildingType && Entity.data.building.business.IsValid;
			string text = BuildingUtil.FindBuildingName(Entity);
			string text2 = "";
			string text3 = "";
			if (flag)
			{
				if (isResidenceBuildingType)
				{
					text2 = Entity.config.residence.GetDesc();
					int num = 5;
					string text4 = string.Join(", ", Entity.components.residence.ProduceResidentNames(num));
					int count = Entity.data.residence.apartments.Count;
					text3 = ((count == 0) ? "" : ((count > num) ? Loc.Get("ui.hud.known.resfooter.many", "names", text4) : Loc.GetPluralized("ui.hud.known.resfooter", count, "names", text4)));
				}
				if (flag2)
				{
					text2 = Loc.Get((Entity.config.board.lotsize.Area <= 6f) ? "ui.hud.known.bizsmall" : "ui.hud.known.bizlarge");
				}
				if (isCivicBuildingType)
				{
					text2 = Entity.config.civic.GetDesc();
				}
			}
			if (!flag)
			{
				if (isResidenceBuildingType)
				{
					text = Loc.Get("ui.hud.unknown.resname");
				}
				if (flag2)
				{
					text = Loc.Get("ui.hud.unknown.bizname");
				}
				if (isCivicBuildingType)
				{
					text = Loc.Get("ui.hud.unknown.civicname");
				}
				text2 = Loc.Get("ui.hud.unknown.notexplored");
			}
			return Loc.Get("ui.hud.unknown", "name", text, "desc", text2, "footer", text3).TrimEnd();
		}
	}

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.None;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.HUDUnknownBuildingInfo;

	internal override void Initialize()
	{
		base.Initialize();
		Game.ctx.events.AddListener(SessionEventType.SelectionFocusChange, OnFocusChange);
		Game.serv.mouseovers.Register(MouseoverType.UnknownBuildingMouseover, new UnknownBuildingMouseover(this));
	}

	internal override void Release()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.UnknownBuildingMouseover);
		Game.ctx.events.RemoveListener(SessionEventType.SelectionFocusChange, OnFocusChange);
		base.Release();
	}

	private void OnFocusChange(SessionEvent sev)
	{
		Entity entity = sev.ctx as Entity;
		if (_entity != null && entity != null && entity == _entity)
		{
			Game.ctx.selection.ClearActive();
		}
	}

	protected override void OnAfterShow()
	{
		if (_entity != null)
		{
			Vector2 value = new Vector2(10f, -10f);
			Game.serv.mouseovers.OnMouseOverOrMove(MouseoverType.UnknownBuildingMouseover, _go, forceRefresh: true, value);
		}
	}

	protected override void OnBeforeHide()
	{
		Game.serv.mouseovers.OnMouseOut(MouseoverType.UnknownBuildingMouseover);
	}
}
