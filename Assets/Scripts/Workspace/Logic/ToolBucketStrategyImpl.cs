using UnityEngine;
using System.Collections;


public class ToolBucketStrategyImpl : ToolLogicStrategy {

	CanvasController canvas;

	public ToolBucketStrategyImpl(){
		canvas = PropertiesSingleton.instance.canvasWorkspaceController.canvas;
	}


	#region ToolLogicStrategy implementation

#if !UNITY_IPHONE

	public void onMouseDown (IntVector2 position)
	{
	}

	public void onMouseOverWithButton (IntVector2 position, Vector3 globalPosition)
	{
	}

	public void onMouseOverWithButtonDone (IntVector2 position)
	{
		doFloodFill(position);
	}

	public void onMouseEnter ()
	{
	}

	public void onMouseExit ()
	{	
	}

	public void onMouseOver (IntVector2 pixelCursorPosition, Vector3 globalCursorPosition)
	{
	}

#else
	IntVector2 point;
	public void onTouchStart (IntVector2 pixelPosition, Vector3 globalPosition)
	{
		point = pixelPosition;
	}

	public void onTouchOver (IntVector2 pixelPosition, Vector3 globalPosition)
	{
		point = pixelPosition;
	}

	public void onTouchEnd ()
	{
		Handheld.StartActivityIndicator();
		doFloodFill (point);
		Handheld.StopActivityIndicator();
	}
#endif

	void doFloodFill(IntVector2 position){
		int x = position.x;
		int y = position.y;
		if (x == ColoringLevelRuntime.Width)
			x = ColoringLevelRuntime.Width - 1;
		if (y == ColoringLevelRuntime.Height)
			y = ColoringLevelRuntime.Height - 1;

		int regionId = ColoringLevelRuntime.RegionAt(x, y);
		int[] pixels = ColoringLevelRuntime.PixelsForRegion(regionId);
		if (regionId == 0 || pixels == null || pixels.Length == 0){
			if (WorkspaceEventManager.instance.onWrongAction!=null)
				WorkspaceEventManager.instance.onWrongAction("persistentborder click");
			return;
		}

		ColoringLevelRuntime.BeginFill(regionId);
		Color32[] current = canvas.actualColors;
		Color32[] targets = new Color32[pixels.Length];
		bool changed = false;
		for (int i = 0; i < pixels.Length; i++) {
			int index = pixels[i];
			Color32 target = ColoringLevelRuntime.ColorAtPixel(index);
			targets[i] = target;
			if (current[index].r != target.r || current[index].g != target.g || current[index].b != target.b)
				changed = true;
		}
		if (!changed)
			return;

		Color32[] colors = canvas.fetchColors();
		for (int i = 0; i < pixels.Length; i++) {
			int index = pixels[i];
			colors[index].r = targets[i].r;
			colors[index].g = targets[i].g;
			colors[index].b = targets[i].b;
			colors[index].a = 255;
		}
		canvas.applyColors(colors,true,true,position);
	}
	#endregion
}
