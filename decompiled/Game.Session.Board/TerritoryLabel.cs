using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Player;
using TMPro;
using UnityEngine;

namespace Game.Session.Board;

public class TerritoryLabel : MonoBehaviour
{
	private const float LABEL_HEIGHT = -6f;

	private TextMeshProUGUI _text;

	private PlayerID _pid = PlayerID.INVALID;

	private WorldPos _districtPos;

	private string _message;

	public void Create(Canvas parent)
	{
		GameObject gameObject = Object.Instantiate(Resources.Load("Shared Components/Map Text Label"), parent.transform) as GameObject;
		gameObject.layer = LayerMask.NameToLayer("Territory");
		_text = gameObject.GetComponent<TextMeshProUGUI>();
		Show(showText: false);
	}

	public void Initialize(PlayerID pid)
	{
		_pid = pid;
		_message = pid.FindPlayer().social.PlayerLastName;
		_text.text = _message;
	}

	internal void Initialize(DistrictConfig dist)
	{
		_message = dist.GetName() ?? "";
		_districtPos = dist.start;
		_text.text = Loc.Get("districts.header", "districtname", _message);
	}

	public void Show(bool showText)
	{
		if (_text != null)
		{
			_text.gameObject.SetActive(showText);
			if (showText)
			{
				SetPosition(_pid.IsAnyPlayer ? FindPlayerCentroid() : FindDistrictCentroid());
			}
		}
		WorldPos FindDistrictCentroid()
		{
			return _districtPos;
		}
		WorldPos FindPlayerCentroid()
		{
			WorldPos worldPos = new WorldPos(0f, 0f);
			int num = 0;
			foreach (NodeID item in _pid.FindPlayer().territory.GetAllOwnedNodesUnsafe())
			{
				Node node = item.FindNode();
				if (node != null)
				{
					worldPos += node.pos;
					num++;
				}
			}
			if (num == 0)
			{
				return default(WorldPos);
			}
			return worldPos / num;
		}
	}

	public void SetPosition(WorldPos pos)
	{
		if (_text != null)
		{
			Vector2 asVector = pos.AsVector2;
			_text.transform.localRotation = Quaternion.identity;
			_text.transform.localPosition = new Vector3(asVector.x, asVector.y, -6f);
		}
	}

	public void SetAlpha(float alpha)
	{
		if (_text != null)
		{
			_text.alpha = alpha;
		}
	}
}
