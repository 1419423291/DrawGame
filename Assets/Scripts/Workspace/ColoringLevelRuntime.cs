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
	static int[] regionPaletteIds;
	static int[] paletteIds;
	static Color32[] colorPixels;
	static Texture2D[] previews;
	static FillPaint fillPaint;

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
		regionPaletteIds = null;
		paletteIds = null;
		colorPixels = null;
		previews = null;
	}

	public static bool Load(ColoringLevel level) {
		Clear();
		if (level == null || level.lineArt == null || level.regionIdMap == null || level.colorImage == null || level.levelData == null) {
			Debug.LogError("Coloring level is missing line art, region map, color image, or level data.");
			return false;
		}
		if (!level.regionIdMap.isReadable) {
			Debug.LogError("regionId_map must be Read/Write enabled.");
			return false;
		}
		if (!level.colorImage.isReadable) {
			Debug.LogError("GCImg_color must be Read/Write enabled.");
			return false;
		}

		int width = level.regionIdMap.width;
		int height = level.regionIdMap.height;
		if (level.lineArt.width != width || level.lineArt.height != height) {
			Debug.LogError("Line art size does not match the region map.");
			return false;
		}
		if (level.colorImage.width != width || level.colorImage.height != height) {
			Debug.LogError("Color image size does not match the region map.");
			return false;
		}

		ColoringLevelFile file = JsonUtility.FromJson<ColoringLevelFile>(level.levelData.text);
		if (file == null || file.paletteItems == null || file.regions == null) {
			Debug.LogError("Failed to read level_data.json.");
			return false;
		}
		if (file.schemaVersion != 3) {
			Debug.LogError("level_data.json schemaVersion must be 3.");
			return false;
		}
		if (file.canvasWidth != width || file.canvasHeight != height)
			Debug.LogWarning("level_data canvas size does not match the region map. Using the texture size.");

		Color32[] palette;
		int[] itemIds;
		if (!BuildPalette(file, out palette, out itemIds))
			return false;

		int[] paletteByRegion;
		if (!BuildRegionPalette(file, itemIds, out paletteByRegion))
			return false;

		byte[] ids = new byte[width * height];
		List<int>[] buckets = new List<int>[256];
		Color32[] regionMap = level.regionIdMap.GetPixels32();
		for (int i = 0; i < regionMap.Length; i++) {
			if (regionMap[i].a == 0 || regionMap[i].r == 0)
				continue;
			byte id = regionMap[i].r;
			if (paletteByRegion[id] == 0) {
				Debug.LogError("Region " + id + " is in the region map but missing from level_data.json.");
				return false;
			}
			ids[i] = id;
			if (buckets[id] == null)
				buckets[id] = new List<int>();
			buckets[id].Add(i);
		}

		int[][] pixelsByRegion = new int[256][];
		for (int id = 1; id < buckets.Length; id++) {
			if (buckets[id] != null)
				pixelsByRegion[id] = buckets[id].ToArray();
		}

		Texture2D[] swatches = new Texture2D[palette.Length];
		for (int i = 0; i < palette.Length; i++)
			swatches[i] = CreateSwatch(palette[i]);

		Width = width;
		Height = height;
		Palette = palette;
		paletteIds = itemIds;
		previews = swatches;
		regionIds = ids;
		regionPixels = pixelsByRegion;
		regionPaletteIds = paletteByRegion;
		colorPixels = level.colorImage.GetPixels32();
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
		if (previews == null || index < 0 || index >= previews.Length)
			return null;
		return previews[index];
	}

	public static bool SelectStyle(int index) {
		if (Palette == null || index < 0 || index >= Palette.Length)
			return false;
		ActiveStyleIndex = index;
		return true;
	}

	public static void BeginFill(int regionId) {
		fillPaint = new FillPaint();
		if (Palette == null || paletteIds == null || ActiveStyleIndex < 0 || ActiveStyleIndex >= Palette.Length)
			return;
		fillPaint.ready = true;
		fillPaint.solid = Palette[ActiveStyleIndex];
		int regionPaletteId = 0;
		if (regionPaletteIds != null && regionId > 0 && regionId < regionPaletteIds.Length)
			regionPaletteId = regionPaletteIds[regionId];
		fillPaint.copySource = regionPaletteId != 0 && regionPaletteId == paletteIds[ActiveStyleIndex];
	}

	public static Color32 ColorAtPixel(int pixelIndex) {
		if (!fillPaint.ready || !fillPaint.copySource || colorPixels == null || pixelIndex < 0 || pixelIndex >= colorPixels.Length)
			return fillPaint.solid;
		Color32 source = colorPixels[pixelIndex];
		source.a = 255;
		return source;
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

	static bool BuildPalette(ColoringLevelFile file, out Color32[] palette, out int[] itemIds) {
		palette = null;
		itemIds = null;
		if (file.paletteItems.Length == 0) {
			Debug.LogError("Coloring level palette is empty.");
			return false;
		}
		palette = new Color32[file.paletteItems.Length];
		itemIds = new int[file.paletteItems.Length];
		for (int i = 0; i < file.paletteItems.Length; i++) {
			ColoringPaletteItemEntry item = file.paletteItems[i];
			if (item == null || item.paletteItemId != i + 1) {
				Debug.LogError("Palette items must use paletteItemId 1..n in array order.");
				return false;
			}
			Color32 color;
			if (!TryParseHex(item.color, out color)) {
				Debug.LogError("Palette item " + item.paletteItemId + " has no usable color.");
				return false;
			}
			palette[i] = color;
			itemIds[i] = item.paletteItemId;
		}
		return true;
	}

	static bool BuildRegionPalette(ColoringLevelFile file, int[] itemIds, out int[] paletteByRegion) {
		paletteByRegion = new int[256];
		bool[] knownItem = new bool[itemIds.Length + 1];
		for (int i = 0; i < itemIds.Length; i++)
			knownItem[itemIds[i]] = true;
		bool[] seen = new bool[256];
		for (int i = 0; i < file.regions.Length; i++) {
			ColoringRegionEntry region = file.regions[i];
			if (region == null)
				continue;
			if (region.regionId <= 0 || region.regionId > 255) {
				Debug.LogError("Region id " + region.regionId + " is outside 1..255.");
				return false;
			}
			if (seen[region.regionId]) {
				Debug.LogError("Region id " + region.regionId + " is duplicated.");
				return false;
			}
			seen[region.regionId] = true;
			if (region.paletteItemId <= 0 || region.paletteItemId >= knownItem.Length || !knownItem[region.paletteItemId]) {
				Debug.LogError("Region " + region.regionId + " references missing palette item " + region.paletteItemId + ".");
				return false;
			}
			paletteByRegion[region.regionId] = region.paletteItemId;
		}
		return true;
	}

	static Texture2D CreateSwatch(Color32 color) {
		const int size = 8;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.filterMode = FilterMode.Point;
		texture.hideFlags = HideFlags.HideAndDontSave;
		Color32[] pixels = new Color32[size * size];
		for (int i = 0; i < pixels.Length; i++)
			pixels[i] = color;
		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		return texture;
	}

	static void DestroyPreviews() {
		if (previews == null)
			return;
		for (int i = 0; i < previews.Length; i++) {
			if (previews[i] != null)
				UnityEngine.Object.Destroy(previews[i]);
		}
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

	struct FillPaint {
		public bool ready;
		public bool copySource;
		public Color32 solid;
	}
}

[Serializable]
public class ColoringLevelFile {
	public int schemaVersion;
	public int canvasWidth;
	public int canvasHeight;
	public ColoringRegionEntry[] regions;
	public ColoringPaletteItemEntry[] paletteItems;
}

[Serializable]
public class ColoringRegionEntry {
	public int regionId;
	public int paletteItemId;
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
	public string color;
}
