using System;
using System.Collections.Generic;
using UnityEngine;

public static class ColoringLevelRuntime {
	public static bool IsLoaded;
	public static int Width;
	public static int Height;
	public static Color32[] Palette;
	public static int ActiveStyleIndex;

	static byte[] regionIds;
	static int[][] regionPixels;
	static RegionBox[] regionBoxes;
	static PaletteStyle[] styles;
	static FillSpan fillSpan;

	public static int PaletteCount {
		get { return Palette != null ? Palette.Length : 0; }
	}

	public static void Clear() {
		DestroyPreviews();
		IsLoaded = false;
		Width = 0;
		Height = 0;
		Palette = null;
		ActiveStyleIndex = 0;
		regionIds = null;
		regionPixels = null;
		regionBoxes = null;
		styles = null;
	}

	public static bool Load(ColoringLevel level) {
		Clear();
		if (level == null || level.lineArt == null || level.regionIdMap == null || level.levelData == null) {
			Debug.LogError("Coloring level is missing line art, region map, or level data.");
			return false;
		}
		if (!level.regionIdMap.isReadable) {
			Debug.LogError("regionId_map must be Read/Write enabled.");
			return false;
		}

		int width = level.regionIdMap.width;
		int height = level.regionIdMap.height;
		if (level.lineArt.width != width || level.lineArt.height != height) {
			Debug.LogError("Line art size does not match the region map.");
			return false;
		}

		ColoringLevelFile file = JsonUtility.FromJson<ColoringLevelFile>(level.levelData.text);
		if (file == null || file.styles == null || file.paletteItems == null) {
			Debug.LogError("Failed to read level_data.json.");
			return false;
		}
		if (file.canvasWidth != width || file.canvasHeight != height)
			Debug.LogWarning("level_data canvas size does not match the region map. Using the texture size.");

		PaletteStyle[] builtStyles = BuildStyles(file);
		if (builtStyles == null || builtStyles.Length == 0) {
			Debug.LogError("Coloring level palette is empty.");
			return false;
		}

		RegionBox[] boxes = new RegionBox[256];
		if (file.regions != null) {
			for (int i = 0; i < file.regions.Length; i++) {
				ColoringRegionEntry region = file.regions[i];
				if (region == null || region.bbox == null)
					continue;
				if (region.regionId <= 0 || region.regionId >= boxes.Length)
					continue;
				if (region.bbox.w <= 0 || region.bbox.h <= 0)
					continue;
				boxes[region.regionId] = new RegionBox(region.bbox.x, region.bbox.y, region.bbox.w, region.bbox.h, true);
			}
		}

		byte[] ids = new byte[width * height];
		List<int>[] buckets = new List<int>[256];
		int[] minX = new int[256];
		int[] minY = new int[256];
		int[] maxX = new int[256];
		int[] maxY = new int[256];
		for (int i = 1; i < 256; i++) {
			minX[i] = int.MaxValue;
			minY[i] = int.MaxValue;
			maxX[i] = int.MinValue;
			maxY[i] = int.MinValue;
		}

		Color32[] pixels = level.regionIdMap.GetPixels32();
		for (int i = 0; i < pixels.Length; i++) {
			if (pixels[i].a == 0 || pixels[i].r == 0)
				continue;
			byte id = pixels[i].r;
			ids[i] = id;
			if (buckets[id] == null)
				buckets[id] = new List<int>();
			buckets[id].Add(i);
			if (!boxes[id].fromJson) {
				int px = i % width;
				int imageY = height - 1 - (i / width);
				if (px < minX[id]) minX[id] = px;
				if (imageY < minY[id]) minY[id] = imageY;
				if (px > maxX[id]) maxX[id] = px;
				if (imageY > maxY[id]) maxY[id] = imageY;
			}
		}

		int[][] pixelsByRegion = new int[256][];
		for (int id = 1; id < buckets.Length; id++) {
			if (buckets[id] == null)
				continue;
			pixelsByRegion[id] = buckets[id].ToArray();
			if (!boxes[id].fromJson && minX[id] != int.MaxValue)
				boxes[id] = new RegionBox(minX[id], minY[id], maxX[id] - minX[id] + 1, maxY[id] - minY[id] + 1, false);
		}

		Color32[] palette = new Color32[builtStyles.Length];
		for (int i = 0; i < builtStyles.Length; i++) {
			builtStyles[i].preview = CreatePreview(builtStyles[i]);
			palette[i] = builtStyles[i].isGradient ? Lerp(builtStyles[i].start, builtStyles[i].end, 0.5f) : builtStyles[i].solid;
		}

		Width = width;
		Height = height;
		Palette = palette;
		styles = builtStyles;
		regionIds = ids;
		regionPixels = pixelsByRegion;
		regionBoxes = boxes;
		IsLoaded = true;
		ActiveStyleIndex = 0;
		ApplyPaletteToProperties();
		return true;
	}

	public static int RegionAt(int x, int y) {
		if (!IsLoaded || x < 0 || y < 0 || x >= Width || y >= Height)
			return 0;
		return regionIds[y * Width + x];
	}

	public static int[] PixelsForRegion(int regionId) {
		if (!IsLoaded || regionId <= 0 || regionId >= regionPixels.Length)
			return null;
		return regionPixels[regionId];
	}

	public static Texture2D PreviewAt(int index) {
		if (styles == null || index < 0 || index >= styles.Length)
			return null;
		return styles[index].preview;
	}

	public static bool SelectStyle(int index) {
		if (styles == null || index < 0 || index >= styles.Length)
			return false;
		ActiveStyleIndex = index;
		return true;
	}

	public static void BeginFill(int regionId) {
		fillSpan = new FillSpan();
		if (styles == null || ActiveStyleIndex < 0 || ActiveStyleIndex >= styles.Length)
			return;
		PaletteStyle style = styles[ActiveStyleIndex];
		fillSpan.ready = true;
		fillSpan.isGradient = style.isGradient;
		fillSpan.solid = style.solid;
		fillSpan.start = style.start;
		fillSpan.end = style.end;
		if (!style.isGradient)
			return;

		RegionBox box = regionBoxes != null && regionId > 0 && regionId < regionBoxes.Length
			? regionBoxes[regionId]
			: new RegionBox();
		if (box.w <= 0 || box.h <= 0)
			box = new RegionBox(0, 0, Width, Height, false);

		float rad = style.angleDeg * Mathf.Deg2Rad;
		float dirX = Mathf.Cos(rad);
		float dirY = Mathf.Sin(rad);
		float minProj;
		float maxProj;
		ProjectBox(box, dirX, dirY, out minProj, out maxProj);
		fillSpan.dirX = dirX;
		fillSpan.dirY = dirY;
		fillSpan.minProj = minProj;
		float span = maxProj - minProj;
		fillSpan.span = span < 0.0001f ? 0.0001f : span;
	}

	public static Color32 ColorAtPixel(int pixelIndex) {
		if (!fillSpan.ready || !fillSpan.isGradient)
			return fillSpan.solid;
		int px = pixelIndex % Width;
		int imageY = Height - 1 - (pixelIndex / Width);
		float t = (px * fillSpan.dirX + imageY * fillSpan.dirY - fillSpan.minProj) / fillSpan.span;
		if (t < 0f)
			t = 0f;
		else if (t > 1f)
			t = 1f;
		return Lerp(fillSpan.start, fillSpan.end, t);
	}

	public static void ApplyPaletteToProperties() {
		PropertiesSingleton props = PropertiesSingleton.instance;
		if (props == null || props.colorProperties == null || Palette == null || Palette.Length == 0)
			return;
		props.colorProperties.randomEnabled = false;
		props.colorProperties.predefinedColors = Palette;
		props.colorProperties.predefinedColorsNumber = Palette.Length;
		ActiveStyleIndex = 0;
		props.colorProperties.activeColor = Palette[0];
	}

	static PaletteStyle[] BuildStyles(ColoringLevelFile file) {
		Dictionary<int, PaletteStyle> byId = new Dictionary<int, PaletteStyle>();
		for (int i = 0; i < file.styles.Length; i++) {
			ColoringStyleEntry entry = file.styles[i];
			if (entry == null)
				continue;
			PaletteStyle style = CreateStyle(entry);
			if (style == null)
				continue;
			byId[entry.styleId] = style;
		}

		List<PaletteStyle> palette = new List<PaletteStyle>();
		for (int i = 0; i < file.paletteItems.Length; i++) {
			ColoringPaletteItemEntry item = file.paletteItems[i];
			if (item == null)
				continue;
			PaletteStyle style;
			if (!byId.TryGetValue(item.styleId, out style)) {
				Debug.LogError("Palette item " + item.paletteItemId + " references missing style " + item.styleId + ".");
				continue;
			}
			palette.Add(style);
		}
		return palette.ToArray();
	}

	static PaletteStyle CreateStyle(ColoringStyleEntry entry) {
		PaletteStyle style = new PaletteStyle();
		if (entry.type == "linearGradient") {
			Color32 start;
			Color32 end;
			if (!TryParseHex(entry.colorStart, out start) || !TryParseHex(entry.colorEnd, out end)) {
				Debug.LogError("Style " + entry.styleId + " has no usable gradient colors.");
				return null;
			}
			style.isGradient = true;
			style.start = start;
			style.end = end;
			style.angleDeg = entry.angleDeg;
			style.solid = Lerp(start, end, 0.5f);
			return style;
		}
		if (entry.type != "solid" && !string.IsNullOrEmpty(entry.type)) {
			Debug.LogError("Style " + entry.styleId + " has unsupported type " + entry.type + ".");
			return null;
		}
		Color32 color;
		if (!TryParseHex(entry.color, out color)) {
			Debug.LogError("Style " + entry.styleId + " has no usable color.");
			return null;
		}
		style.solid = color;
		return style;
	}

	static void ProjectBox(RegionBox box, float dirX, float dirY, out float minProj, out float maxProj) {
		float x1 = box.x + box.w;
		float y1 = box.y + box.h;
		minProj = maxProj = box.x * dirX + box.y * dirY;
		Consider(x1, box.y, dirX, dirY, ref minProj, ref maxProj);
		Consider(box.x, y1, dirX, dirY, ref minProj, ref maxProj);
		Consider(x1, y1, dirX, dirY, ref minProj, ref maxProj);
	}

	static void Consider(float x, float y, float dirX, float dirY, ref float minProj, ref float maxProj) {
		float proj = x * dirX + y * dirY;
		if (proj < minProj)
			minProj = proj;
		if (proj > maxProj)
			maxProj = proj;
	}

	static Texture2D CreatePreview(PaletteStyle style) {
		const int width = 64;
		const int height = 32;
		Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.filterMode = FilterMode.Bilinear;
		texture.hideFlags = HideFlags.HideAndDontSave;
		Color32[] pixels = new Color32[width * height];
		if (!style.isGradient) {
			for (int i = 0; i < pixels.Length; i++)
				pixels[i] = style.solid;
		} else {
			RegionBox box = new RegionBox(0, 0, width, height, false);
			float rad = style.angleDeg * Mathf.Deg2Rad;
			float dirX = Mathf.Cos(rad);
			float dirY = Mathf.Sin(rad);
			float minProj;
			float maxProj;
			ProjectBox(box, dirX, dirY, out minProj, out maxProj);
			float span = maxProj - minProj;
			if (span < 0.0001f)
				span = 0.0001f;
			for (int py = 0; py < height; py++) {
				int imageY = height - 1 - py;
				for (int px = 0; px < width; px++) {
					float t = (px * dirX + imageY * dirY - minProj) / span;
					if (t < 0f)
						t = 0f;
					else if (t > 1f)
						t = 1f;
					pixels[py * width + px] = Lerp(style.start, style.end, t);
				}
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		return texture;
	}

	static void DestroyPreviews() {
		if (styles == null)
			return;
		for (int i = 0; i < styles.Length; i++) {
			if (styles[i] != null && styles[i].preview != null)
				UnityEngine.Object.Destroy(styles[i].preview);
		}
	}

	static Color32 Lerp(Color32 a, Color32 b, float t) {
		return new Color32(
			(byte)(a.r + (b.r - a.r) * t),
			(byte)(a.g + (b.g - a.g) * t),
			(byte)(a.b + (b.b - a.b) * t),
			255);
	}

	static bool TryParseHex(string hex, out Color32 color) {
		color = new Color32(0, 0, 0, 255);
		if (string.IsNullOrEmpty(hex))
			return false;
		if (hex[0] == '#')
			hex = hex.Substring(1);
		if (hex.Length != 6)
			return false;
		try {
			color.r = Convert.ToByte(hex.Substring(0, 2), 16);
			color.g = Convert.ToByte(hex.Substring(2, 2), 16);
			color.b = Convert.ToByte(hex.Substring(4, 2), 16);
			color.a = 255;
			return true;
		} catch (Exception) {
			return false;
		}
	}

	struct RegionBox {
		public int x;
		public int y;
		public int w;
		public int h;
		public bool fromJson;

		public RegionBox(int x, int y, int w, int h, bool fromJson) {
			this.x = x;
			this.y = y;
			this.w = w;
			this.h = h;
			this.fromJson = fromJson;
		}
	}

	class PaletteStyle {
		public bool isGradient;
		public Color32 solid;
		public Color32 start;
		public Color32 end;
		public float angleDeg;
		public Texture2D preview;
	}

	struct FillSpan {
		public bool ready;
		public bool isGradient;
		public Color32 solid;
		public Color32 start;
		public Color32 end;
		public float dirX;
		public float dirY;
		public float minProj;
		public float span;
	}
}

[Serializable]
public class ColoringLevelFile {
	public int canvasWidth;
	public int canvasHeight;
	public ColoringStyleEntry[] styles;
	public ColoringRegionEntry[] regions;
	public ColoringPaletteItemEntry[] paletteItems;
}

[Serializable]
public class ColoringStyleEntry {
	public int styleId;
	public string type;
	public string color;
	public string colorStart;
	public string colorEnd;
	public float angleDeg;
}

[Serializable]
public class ColoringRegionEntry {
	public int regionId;
	public ColoringBBox bbox;
}

[Serializable]
public class ColoringBBox {
	public int x;
	public int y;
	public int w;
	public int h;
}

[Serializable]
public class ColoringPaletteItemEntry {
	public int paletteItemId;
	public int styleId;
}
