using Game.Services.Audio;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Session.Tickers;

public abstract class BaseTicker
{
	internal class Context : MonoBehaviour
	{
		public BaseTicker ticker;
	}

	public TickerData data;

	public GameObject go;

	public void Initialize(TickerData info, GameObject go)
	{
		data = info;
		this.go = go;
		go.AddComponent<Context>().ticker = this;
		go.GetComponent<Button>().onClick.SetListener(OnClick);
		go.SetText("Text", data.icon.GetIcon());
		go.GetComponent<PlayUISound>().SetEffectGenerator((PointerEventData data) => (data.button != PointerEventData.InputButton.Right) ? SFXType.TickerClick : SFXType.TickerClose);
	}

	public void Release()
	{
		go = null;
		data = null;
	}

	private void OnClick()
	{
		if (HandleClick())
		{
			Game.ctx.hud.tickers.RemoveTicker(data);
		}
	}

	public Entity FindTarget()
	{
		return data?.target.entityId.FindEntity();
	}

	protected abstract bool HandleClick();
}
