using System.IO;
using UnityEditor;
using UnityEngine;

public class LevelArtTextureImporter : AssetPostprocessor {
	const string PicturesRoot = "Assets/Textures/Pictures";

	[InitializeOnLoadMethod]
	static void ScheduleExistingArtFix() {
		EditorApplication.delayCall += FixExistingLevelArt;
	}

	void OnPreprocessTexture() {
		TextureImporter importer = assetImporter as TextureImporter;
		if (importer == null || !TryGetKind(importer.assetPath, out ArtKind kind))
			return;
		Apply(importer, kind);
	}

	static void FixExistingLevelArt() {
		if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
			EditorApplication.delayCall += FixExistingLevelArt;
			return;
		}
		if (EditorApplication.isPlayingOrWillChangePlaymode || !Directory.Exists(PicturesRoot))
			return;

		string[] directories = Directory.GetDirectories(PicturesRoot);
		for (int i = 0; i < directories.Length; i++) {
			if (!Path.GetFileName(directories[i]).StartsWith("level"))
				continue;
			string assetDir = directories[i].Replace('\\', '/');
			ReimportIfNeeded(assetDir + "/regionId_map.png");
			ReimportIfNeeded(assetDir + "/lineart.png");
			ReimportIfNeeded(assetDir + "/GCImg_color.png");
		}
	}

	static void ReimportIfNeeded(string assetPath) {
		TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
		ArtKind kind;
		if (importer == null || !TryGetKind(assetPath, out kind))
			return;
		if (Apply(importer, kind))
			importer.SaveAndReimport();
	}

	static bool TryGetKind(string assetPath, out ArtKind kind) {
		kind = ArtKind.None;
		if (string.IsNullOrEmpty(assetPath))
			return false;
		string path = assetPath.Replace('\\', '/');
		if (path.IndexOf("/Pictures/level") < 0)
			return false;
		string name = Path.GetFileName(path);
		if (name == "regionId_map.png") {
			kind = ArtKind.Region;
			return true;
		}
		if (name == "lineart.png") {
			kind = ArtKind.LineArt;
			return true;
		}
		if (name == "GCImg_color.png") {
			kind = ArtKind.Color;
			return true;
		}
		return false;
	}

	static bool Apply(TextureImporter importer, ArtKind kind) {
		if (kind == ArtKind.Region)
			return ApplyRegion(importer);
		if (kind == ArtKind.LineArt)
			return ApplyLineArt(importer);
		if (kind == ArtKind.Color)
			return ApplyColor(importer);
		return false;
	}

	static bool ApplyRegion(TextureImporter importer) {
		bool changed = ApplyCommon(importer, true, false, FilterMode.Point, false, TextureImporterCompression.Uncompressed);
		changed |= SetAlphaIsTransparency(importer, false);
		return changed;
	}

	static bool ApplyLineArt(TextureImporter importer) {
		bool changed = ApplyCommon(importer, true, true, FilterMode.Bilinear, true, TextureImporterCompression.Compressed);
		changed |= SetAlphaIsTransparency(importer, true);
		return changed;
	}

	static bool ApplyColor(TextureImporter importer) {
		bool changed = ApplyCommon(importer, true, false, FilterMode.Point, true, TextureImporterCompression.Uncompressed);
		changed |= SetAlphaIsTransparency(importer, false);
		TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
		if (!standalone.overridden
			|| standalone.format != TextureImporterFormat.RGBA32
			|| standalone.textureCompression != TextureImporterCompression.Uncompressed
			|| standalone.maxTextureSize < 2048) {
			standalone.overridden = true;
			standalone.format = TextureImporterFormat.RGBA32;
			standalone.textureCompression = TextureImporterCompression.Uncompressed;
			standalone.maxTextureSize = 2048;
			importer.SetPlatformTextureSettings(standalone);
			changed = true;
		}
		return changed;
	}

	static bool ApplyCommon(TextureImporter importer, bool readable, bool mipmaps, FilterMode filter, bool srgb, TextureImporterCompression compression) {
		bool changed = false;
		if (importer.textureType != TextureImporterType.Default) {
			importer.textureType = TextureImporterType.Default;
			changed = true;
		}
		if (importer.isReadable != readable) {
			importer.isReadable = readable;
			changed = true;
		}
		if (importer.mipmapEnabled != mipmaps) {
			importer.mipmapEnabled = mipmaps;
			changed = true;
		}
		if (importer.filterMode != filter) {
			importer.filterMode = filter;
			changed = true;
		}
		if (importer.npotScale != TextureImporterNPOTScale.None) {
			importer.npotScale = TextureImporterNPOTScale.None;
			changed = true;
		}
		if (importer.sRGBTexture != srgb) {
			importer.sRGBTexture = srgb;
			changed = true;
		}
		if (importer.textureCompression != compression) {
			importer.textureCompression = compression;
			changed = true;
		}
		if (importer.maxTextureSize < 2048) {
			importer.maxTextureSize = 2048;
			changed = true;
		}
		return changed;
	}

	static bool SetAlphaIsTransparency(TextureImporter importer, bool enabled) {
		if (importer.alphaIsTransparency == enabled)
			return false;
		importer.alphaIsTransparency = enabled;
		return true;
	}

	enum ArtKind {
		None,
		Region,
		LineArt,
		Color
	}
}
