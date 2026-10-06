using UnityEngine;
using System.Collections;
using System;


[Serializable]
public class PredefinedColorsConfig{
	public int left;
	public int down;
	public int totalWidth = 86;
	public int itemWidth;
	public int itemHeight = 37;
	public int colorsNumber = 12;
	public int colorPerRow = 2;
	public int marginY= 0;
	
	public string paletteButtonStyleName = "paletteButton";
	public GUIStyle paletteButtonStyle;
	
	public string leftButtonStyleName = "predefinedColorLeft";
	public Texture2D leftButtonBG;
	public GUIStyle leftButtonStyle;


	public string rightButtonStyleName = "predefinedColorRight";
	public Texture2D rightButtonBG;
	public GUIStyle rightButtonStyle;
	
	public string downLeftButtonStyleName = "predefinedColorDownLeft";
	public Texture2D downLeftButtonBG;

	public GUIStyle downLeftButtonStyle;
	public string downRightButtonStyleName = "predefinedColorDownRight";
	public Texture2D downRightButtonBG;
	public GUIStyle downRightButtonStyle;
	
}


[Serializable]
public class PredefinedColorsAndPalette : GUIPart {
	public PredefinedColorsConfig config;
	public Rect[] itemPositions;
	public Texture2D[]  bgs;
	public Rect palleteButtonRect;
	public GUIStyle[] styles;
	
	
	public void setDownLeftPosition(int x, int y, GUISkin skin){
		setStyle(skin, config.paletteButtonStyleName,   out config.paletteButtonStyle);
		setStyle(skin, config.leftButtonStyleName,      out config.leftButtonStyle);
		setStyle(skin, config.rightButtonStyleName,     out config.rightButtonStyle);
		setStyle(skin, config.downLeftButtonStyleName,  out config.downLeftButtonStyle);
		setStyle(skin, config.downRightButtonStyleName, out config.downRightButtonStyle);
		config.left = x;
		config.down = y;
		recalculatePositions();
	}
	
	
	public void recalculatePositions(){
		int count = config.colorsNumber;
		if (count < 1)
			count = 1;
		itemPositions = new Rect[count];
		styles = new GUIStyle[count];
		bgs = new Texture2D[count];
		config.itemWidth = config.totalWidth / config.colorPerRow;
		int rowNumber = (count + config.colorPerRow - 1) / config.colorPerRow;
		int id=0;
		int y=-1;
		int x=-1;
		bool left = true;
		for (int i = 0; i < rowNumber; i++) {
			y = config.down - config.itemHeight - i*(config.itemHeight + config.marginY );
			for (int j = 0; j < config.colorPerRow && id < count; j++) {
				x = config.left + config.itemWidth*j;
				styles[id] = left ? config.leftButtonStyle : config.rightButtonStyle;
				bgs   [id] = left ? config.leftButtonBG    : config.rightButtonBG   ;
				itemPositions[id++]= new Rect(x,y,config.itemWidth,config.itemHeight);	
				left = !left;
			}
		}

		if (count > 0){
			styles[0] = config.downLeftButtonStyle;
			bgs[0] = config.downLeftButtonBG;
		}
		if (count > 1){
			styles[1] = config.downRightButtonStyle;
			bgs[1] = config.downRightButtonBG;
		}
 	}
	
	
	int i;
	public override void OnGUI (){
		int paletteCount = ColoringLevelRuntime.PaletteCount;
		if (paletteCount > 0 && (itemPositions == null || itemPositions.Length != paletteCount)){
			config.colorsNumber = paletteCount;
			recalculatePositions();
		}
		if (itemPositions == null)
			return;

		Color32[] palette = ColoringLevelRuntime.Palette;
		Color previousColor = GUI.color;
		for (i = 0; i < itemPositions.Length; i++) {
			Texture2D preview = ColoringLevelRuntime.PreviewAt(i);
			if (preview != null) {
				GUI.color = Color.white;
				GUI.DrawTexture(itemPositions[i], preview, ScaleMode.StretchToFill);
			} else {
				if (palette != null && i < palette.Length)
					GUI.color = palette[i];
				else
					GUI.color = PropertiesSingleton.instance.colorProperties.predefinedColors[i];
				GUI.DrawTexture(itemPositions[i],bgs[i]);
			}
			GUI.color = previousColor;
			if (GUI.Button(itemPositions[i],GUIContent.none, styles[i]) && WorkspaceEventManager.instance.onPredefinedColorClick!=null)
				WorkspaceEventManager.instance.onPredefinedColorClick(i);				
		}
	}	
	
	public void setPosition (Rect rect)
	{		
	}	
}
