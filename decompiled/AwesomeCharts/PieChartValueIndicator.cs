using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace AwesomeCharts;

[ExecuteInEditMode]
[RequireComponent(typeof(CanvasRenderer))]
public class PieChartValueIndicator : MonoBehaviour
{
	[SerializeField]
	private string label;

	[SerializeField]
	private int fontSize = 14;

	[SerializeField]
	private Color indicatorColor = Color.white;

	[SerializeField]
	private List<Vector2> linePoints;

	[SerializeField]
	private bool reversedLabel;

	private Text labelText;

	private UILineRenderer lineRenderer;

	private bool isDirty = true;

	private ViewCreator viewCreator = new ViewCreator();

	public string Label
	{
		get
		{
			return label;
		}
		set
		{
			label = value;
			SetDirty();
		}
	}

	public int FontSize
	{
		get
		{
			return fontSize;
		}
		set
		{
			fontSize = value;
			SetDirty();
		}
	}

	public Color IndicatorColor
	{
		get
		{
			return indicatorColor;
		}
		set
		{
			indicatorColor = value;
			SetDirty();
		}
	}

	public List<Vector2> LinePoints
	{
		get
		{
			return linePoints;
		}
		set
		{
			linePoints = value;
			SetDirty();
		}
	}

	public bool ReversedLabel
	{
		get
		{
			return reversedLabel;
		}
		set
		{
			reversedLabel = value;
			SetDirty();
		}
	}

	public void SetDirty()
	{
		isDirty = true;
	}

	private void Awake()
	{
		if (LinePoints == null)
		{
			linePoints = new List<Vector2>();
		}
	}

	private void Start()
	{
		ClearEditModeObjects();
		InstantiateViews();
	}

	private void ClearEditModeObjects()
	{
		int childCount = base.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Object.DestroyImmediate(base.transform.GetChild(0).gameObject);
		}
	}

	private void InstantiateViews()
	{
		labelText = viewCreator.InstantiateText("label", base.transform, PivotValue.MIDDLE_LEFT);
		labelText.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
		labelText.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
		labelText.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 40f);
		lineRenderer = viewCreator.InstantiateLineRenderer("line", base.transform, PivotValue.CENTER);
		lineRenderer.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
		lineRenderer.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
		lineRenderer.lineThickness = 2f;
	}

	private void OnValidate()
	{
		SetDirty();
	}

	private void Update()
	{
		if (isDirty)
		{
			UpdateView();
			isDirty = false;
		}
	}

	private void UpdateView()
	{
		if (!(labelText == null) && !(lineRenderer == null))
		{
			labelText.text = Label;
			labelText.color = IndicatorColor;
			labelText.fontSize = FontSize;
			lineRenderer.Points = LinePoints.ToArray();
			lineRenderer.color = IndicatorColor;
			if (ReversedLabel)
			{
				labelText.alignment = TextAnchor.MiddleRight;
				labelText.GetComponent<RectTransform>().pivot = PivotValue.MIDDLE_RIGHT;
			}
			else
			{
				labelText.alignment = TextAnchor.MiddleLeft;
				labelText.GetComponent<RectTransform>().pivot = PivotValue.MIDDLE_LEFT;
			}
		}
	}
}
