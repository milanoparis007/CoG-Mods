using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace AwesomeCharts;

public class ViewCreator : Object
{
	private GameObject CreateBaseGameObject(string name, Transform parent, Vector2 pivot)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.HideInInspector;
		RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 0f);
		rectTransform.anchorMax = new Vector2(0f, 0f);
		rectTransform.pivot = pivot;
		rectTransform.anchoredPosition = new Vector3(0f, 0f, 0f);
		return gameObject;
	}

	public T InstantiateWithPrefab<T>(T prefab, Transform parent) where T : Object
	{
		T val = Object.Instantiate(prefab, parent, worldPositionStays: false);
		val.hideFlags = HideFlags.DontSave;
		return val;
	}

	public ScrollRect InstantiateScroll(string name, Transform parent, Vector2 pivot)
	{
		GameObject gameObject = CreateBaseGameObject(name, parent, pivot);
		gameObject.AddComponent<CanvasRenderer>();
		ScrollRect scrollRect = gameObject.AddComponent<ScrollRect>();
		scrollRect.viewport = InstantiateViewPort(gameObject.transform);
		scrollRect.content = InstantiateContentView(scrollRect.viewport).GetComponent<RectTransform>();
		scrollRect.vertical = false;
		return scrollRect;
	}

	private RectTransform InstantiateViewPort(Transform parent)
	{
		GameObject gameObject = CreateBaseGameObject("ViewPort", parent, PivotValue.BOTTOM_LEFT);
		gameObject.AddComponent<Image>();
		gameObject.AddComponent<Mask>();
		gameObject.GetComponent<Mask>().showMaskGraphic = false;
		return gameObject.GetComponent<RectTransform>();
	}

	public GameObject InstantiateContentView(Transform parent)
	{
		GameObject gameObject = CreateBaseGameObject("Content", parent, PivotValue.BOTTOM_LEFT);
		gameObject.AddComponent<CanvasRenderer>();
		return gameObject;
	}

	public GameObject InstantiateChartDataContainerView(Transform parent)
	{
		GameObject gameObject = CreateBaseGameObject("DataContent", parent, PivotValue.BOTTOM_LEFT);
		gameObject.AddComponent<CanvasRenderer>();
		return gameObject;
	}

	public Text InstantiateBottomLabel(string name, Transform parent, Vector2 pivot)
	{
		Font font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
		Text text = CreateBaseGameObject(name, parent, pivot).AddComponent<Text>();
		text.font = font;
		text.alignment = TextAnchor.MiddleCenter;
		return text.GetComponent<Text>();
	}

	public GridRenderer InstantiateGridRenderer(string name, Transform parent, Vector2 pivot)
	{
		GameObject gameObject = CreateBaseGameObject(name, parent, pivot);
		gameObject.AddComponent<CanvasRenderer>();
		return gameObject.AddComponent<GridRenderer>();
	}

	public FrameRenderer InstantiateFrameRenderer(string name, Transform parent, Vector2 pivot)
	{
		return CreateBaseGameObject(name, parent, pivot).AddComponent<FrameRenderer>();
	}

	public AxisLabelRenderer InstantiateAxisLabelRenderer(string name, Transform parent, Vector2 pivot)
	{
		return CreateBaseGameObject(name, parent, pivot).AddComponent<AxisLabelRenderer>();
	}

	public UILineRenderer InstantiateLineRenderer(string name, Transform parent, Vector2 pivot)
	{
		UILineRenderer uILineRenderer = CreateBaseGameObject(name, parent, pivot).AddComponent<UILineRenderer>();
		uILineRenderer.raycastTarget = false;
		uILineRenderer.sprite = Resources.Load<Sprite>("sprites/line_fill");
		return uILineRenderer;
	}

	public LineChartBackground InstantiateLineBackground(string name, Transform parent, Vector2 pivot)
	{
		LineChartBackground lineChartBackground = CreateBaseGameObject(name, parent, pivot).AddComponent<LineChartBackground>();
		lineChartBackground.raycastTarget = false;
		return lineChartBackground;
	}

	public LineEntryIdicator InstantiateCircleImage(string name, Transform parent)
	{
		GameObject gameObject = CreateBaseGameObject(name, parent, PivotValue.CENTER);
		LineEntryIdicator lineEntryIdicator = gameObject.AddComponent<LineEntryIdicator>();
		lineEntryIdicator.button = gameObject.AddComponent<Button>();
		lineEntryIdicator.image = gameObject.AddComponent<Image>();
		lineEntryIdicator.image.sprite = Resources.Load<Sprite>("sprites/line_chart_circle");
		lineEntryIdicator.button.targetGraphic = lineEntryIdicator.image;
		return lineEntryIdicator;
	}

	public Bar InstantiateBar(Transform parent, Bar barPrefab)
	{
		return InstantiateWithPrefab(barPrefab ?? Resources.Load<Bar>("prefabs/Bar"), parent);
	}

	public ChartValuePopup InstantiateChartPopup(Transform parent, ChartValuePopup popupPrefab)
	{
		ChartValuePopup chartValuePopup = InstantiateWithPrefab(popupPrefab ?? Resources.Load<ChartValuePopup>("prefabs/ChartValuePopup"), parent);
		chartValuePopup.GetComponent<RectTransform>().pivot = PivotValue.BOTTOM_MIDDLE;
		return chartValuePopup;
	}

	public LegendEntryView InstantiateLegendEntry(Transform parent, Vector2 pivot)
	{
		LegendEntryView legendEntryView = InstantiateWithPrefab(Resources.Load<LegendEntryView>("prefabs/LegendEntryView"), parent);
		RectTransform component = legendEntryView.GetComponent<RectTransform>();
		component.pivot = pivot;
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0f, 0f);
		component.anchoredPosition = Vector3.zero;
		return legendEntryView;
	}

	public PieChartEntryView InstantiatePieChartEntryView(string name, Transform parent, Vector2 pivot)
	{
		PieChartEntryView pieChartEntryView = CreateBaseGameObject(name, parent, pivot).AddComponent<PieChartEntryView>();
		RectTransform component = pieChartEntryView.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		return pieChartEntryView;
	}

	public GameObject InstantiateMaskablePieChartObject(string name, Transform parent, Vector2 pivot)
	{
		Image image = CreateBaseGameObject(name, parent, pivot).AddComponent<Image>();
		image.sprite = Resources.Load<Sprite>("sprites/pie_chart_image");
		image.material = Resources.Load<Material>("materials/reversed_mask_material");
		RectTransform component = image.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		return image.gameObject;
	}

	public PieChartValueIndicator InstantiatePieEntryValueIndicator(string name, Transform parent, Vector2 pivot)
	{
		PieChartValueIndicator pieChartValueIndicator = CreateBaseGameObject(name, parent, pivot).AddComponent<PieChartValueIndicator>();
		pieChartValueIndicator.GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 1f);
		pieChartValueIndicator.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
		pieChartValueIndicator.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
		return pieChartValueIndicator;
	}

	public Text InstantiateText(string name, Transform parent, Vector2 pivot)
	{
		Text text = CreateBaseGameObject(name, parent, pivot).AddComponent<Text>();
		Font font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
		text.font = font;
		return text;
	}
}
