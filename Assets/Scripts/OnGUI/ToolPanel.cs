using UnityEngine;
using System.Collections;
using System;

[Serializable]
public class ToolPanelConfig{
	public int left;
	public int top;
	public int buttonSize = 37;
	public int totalWidth = 86;
	public string styleName = "toolButton";
	public GUIStyle style;
	
	public Texture2D incIcon;
	public Texture2D brushIcon;	
	public Texture2D bigBrushIcon;
	public Texture2D rollerIcon;
	public Texture2D bucketIcon;
	public Texture2D stampIcon;
}


[Serializable]
public class ToolPanel : GUIPart {
	public ToolPanelConfig config;
	Rect bucketRect;

	
	
	public void setTopLeftPosition(int left, int top, GUISkin skin){		
		setStyle(skin, config.styleName, out config.style);
		
		config.left = left;
		config.top = top;
		recalculatePositions();
	}
		
	public override void OnGUI (){
		if (GUI.Button(bucketRect, config.bucketIcon, config.style) && WorkspaceEventManager.instance.onToolButtonClick !=null)
			WorkspaceEventManager.instance.onToolButtonClick(ToolType.BUCKET);
	}	
	
	void recalculatePositions(){
		bucketRect = new Rect(config.left, config.top, config.buttonSize, config.buttonSize);
	}
	
	
	public void setPosition (Rect rect)
	{
		
	}
	
	
}
