using Game.Services;
using UnityEngine;

namespace Game.UI;

public class TabCtx : MonoBehaviour
{
	public ChapterDef chapterDef;

	public int stage;

	public void Set(ChapterDef chapterDef, int stage)
	{
		this.chapterDef = chapterDef;
		this.stage = stage;
	}
}
