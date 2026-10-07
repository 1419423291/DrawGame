using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ColoringLevelCatalogBuilder : AssetPostprocessor {
	const string PicturesRoot = "Assets/Textures/Pictures";
	const string OutputRoot = "Assets/ScriptableObjects/ColoringLevels";
	const string ListPath = "Assets/ScriptableObjects/Konglong/konglong.list.asset";
	const string AlbumsPath = "Assets/ScriptableObjects/Konglong/konglong.albums.asset";

	static bool refreshing;

	[MenuItem("Razukrashka/Refresh Coloring Levels")]
	public static void RefreshMenu() {
		Refresh();
	}

	static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) {
		if (refreshing)
			return;
		if (!TouchesLevelFolder(imported) && !TouchesLevelFolder(deleted) && !TouchesLevelFolder(moved) && !TouchesLevelFolder(movedFrom))
			return;
		Refresh();
	}

	static bool TouchesLevelFolder(string[] paths) {
		if (paths == null)
			return false;
		for (int i = 0; i < paths.Length; i++) {
			string path = paths[i].Replace('\\', '/');
			if (path.IndexOf("/Pictures/level") < 0)
				continue;
			if (path.EndsWith("level_data.json") || path.EndsWith("lineart.png") || path.EndsWith("regionId_map.png")
				|| path.EndsWith("GCImg_color.png")
				|| path.EndsWith("level_data.json.meta") || path.EndsWith("lineart.png.meta") || path.EndsWith("regionId_map.png.meta")
				|| path.EndsWith("GCImg_color.png.meta"))
				return true;
		}
		return false;
	}

	public static void Refresh() {
		if (refreshing)
			return;
		if (!Directory.Exists(PicturesRoot))
			return;
		refreshing = true;
		try {
			List<SheetObject> sheets = ScanLevels();
			ApplyList(sheets);
		} catch (Exception ex) {
			Debug.LogError("Coloring level scan failed. Keeping the existing list. " + ex);
		} finally {
			refreshing = false;
		}
	}

	static List<SheetObject> ScanLevels() {
		if (!AssetDatabase.IsValidFolder(OutputRoot))
			AssetDatabase.CreateFolder("Assets/ScriptableObjects", "ColoringLevels");

		string[] directories = Directory.GetDirectories(PicturesRoot);
		List<string> levelDirs = new List<string>();
		for (int i = 0; i < directories.Length; i++) {
			string name = Path.GetFileName(directories[i]);
			if (name.StartsWith("level"))
				levelDirs.Add(directories[i]);
		}
		levelDirs.Sort(delegate(string a, string b) {
			return string.Compare(Path.GetFileName(a), Path.GetFileName(b), System.StringComparison.Ordinal);
		});

		List<SheetObject> sheets = new List<SheetObject>();
		for (int i = 0; i < levelDirs.Count; i++) {
			SheetObject sheet = BuildLevel(levelDirs[i]);
			if (sheet != null)
				sheets.Add(sheet);
		}
		return sheets;
	}

	static void ApplyList(List<SheetObject> sheets) {
		SheetList list = AssetDatabase.LoadAssetAtPath<SheetList>(ListPath);
		Albums albums = AssetDatabase.LoadAssetAtPath<Albums>(AlbumsPath);
		if (list == null || albums == null || albums.album == null || albums.album.Count < 2) {
			Debug.LogError("Coloring level list or albums asset is missing.");
			return;
		}

		if (sheets.Count == 0)
			Debug.LogWarning("No schema 3 coloring levels found.");

		list.sheetList = sheets.ToArray();
		EditorUtility.SetDirty(list);
		albums.album[1].sheetList = list;
		if (sheets.Count > 0) {
			albums.album[0].sheetObject = sheets[0];
			if (sheets[0].coloringLevel != null && sheets[0].coloringLevel.preview != null)
				albums.album[1].icon = sheets[0].coloringLevel.preview;
		}
		EditorUtility.SetDirty(albums);
		AssetDatabase.SaveAssets();
	}

	static SheetObject BuildLevel(string directory) {
		string folder = Path.GetFileName(directory);
		string assetDir = ToAssetPath(directory);
		string jsonPath = assetDir + "/level_data.json";
		string lineArtPath = assetDir + "/lineart.png";
		string regionPath = assetDir + "/regionId_map.png";
		string colorPath = assetDir + "/GCImg_color.png";
		TextAsset levelData = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
		if (levelData == null
			|| AssetDatabase.LoadAssetAtPath<Texture2D>(lineArtPath) == null
			|| AssetDatabase.LoadAssetAtPath<Texture2D>(regionPath) == null) {
			Debug.LogWarning("Skipping " + folder + " because lineart, regionId_map, or level_data.json is missing.");
			return null;
		}

		ColoringLevelFile file = JsonUtility.FromJson<ColoringLevelFile>(levelData.text);
		if (file == null || file.schemaVersion != 3) {
			int version = file != null ? file.schemaVersion : 0;
			Debug.LogWarning("Skipping " + folder + " because schemaVersion is " + version + ", expected 3.");
			return null;
		}
		if (AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath) == null) {
			Debug.LogWarning("Skipping " + folder + " because GCImg_color.png is missing.");
			return null;
		}

		ColoringLevel level = LoadOrCreate<ColoringLevel>(OutputRoot + "/" + folder + ".level.asset");
		level.levelId = folder;
		level.lineArt = AssetDatabase.LoadAssetAtPath<Texture2D>(lineArtPath);
		level.regionIdMap = AssetDatabase.LoadAssetAtPath<Texture2D>(regionPath);
		level.levelData = levelData;
		level.colorImage = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
		level.preview = level.colorImage;
		EditorUtility.SetDirty(level);

		SheetObject sheet = LoadOrCreate<SheetObject>(OutputRoot + "/" + folder + ".sheet.asset");
		sheet.nameKey = folder;
		sheet.persistentBorderLayerPath = "";
		sheet.persistentFrontOutlinePath = "";
		sheet.coloringLevel = level;
		EditorUtility.SetDirty(sheet);
		return sheet;
	}

	static T LoadOrCreate<T>(string path) where T : ScriptableObject {
		T asset = AssetDatabase.LoadAssetAtPath<T>(path);
		if (asset != null)
			return asset;
		asset = ScriptableObject.CreateInstance<T>();
		AssetDatabase.CreateAsset(asset, path);
		return asset;
	}

	static string ToAssetPath(string path) {
		return path.Replace('\\', '/');
	}
}
