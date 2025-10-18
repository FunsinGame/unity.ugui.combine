using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class BuiltinSpriteExporter
{
    [MenuItem("Tools/UI/Export Builtin UI Sprites")]
    static void ExportBuiltinSprites()
    {
        string outputPath = "Assets/UI/BuiltinSprites";
        
        // 确保目录存在
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // 内置Sprite配置
        var sprites = new (string path, string name)[]
        {
            ("UI/Skin/UISprite.psd", "UISprite"),
            ("UI/Skin/Background.psd", "Background"),
            ("UI/Skin/InputFieldBackground.psd", "InputFieldBackground"),
            ("UI/Skin/Knob.psd", "Knob"),
            ("UI/Skin/Checkmark.psd", "Checkmark"),
            ("UI/Skin/DropdownArrow.psd", "DropdownArrow"),
            ("UI/Skin/UIMask.psd", "UIMask")
        };

        int exportedCount = 0;

        foreach (var (path, name) in sprites)
        {
            try
            {
                // 获取内置Sprite
                Sprite builtinSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
                
                if (builtinSprite != null)
                {
                    Debug.Log($"开始处理内置Sprite: {name} (路径: {path})");
                    Debug.Log($"Sprite信息: 大小={builtinSprite.rect}, 边框={builtinSprite.border}, 纹理={builtinSprite.texture.name}({builtinSprite.texture.width}x{builtinSprite.texture.height})");
                    
                    // 创建PNG文件
                    Texture2D texture = GetTextureFromSprite(builtinSprite);
                    if (texture == null)
                    {
                        Debug.LogError($"无法从Sprite获取纹理: {name}");
                        continue;
                    }
                    
                    byte[] pngBytes = texture.EncodeToPNG();
                    if (pngBytes == null || pngBytes.Length == 0)
                    {
                        Debug.LogError($"无法编码PNG: {name}");
                        Object.DestroyImmediate(texture);
                        continue;
                    }
                    
                    string filePath = Path.Combine(outputPath, $"{name}.png");
                    string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), filePath);
                    File.WriteAllBytes(fullPath, pngBytes);
                    
                    Object.DestroyImmediate(texture);
                    exportedCount++;
                    
                    // 刷新单个资源并设置九宫格信息
                    AssetDatabase.ImportAsset(filePath);
                    SetSpriteBorder(filePath, builtinSprite.border);
                    
                    Debug.Log($"成功导出: {name}.png (文件大小: {pngBytes.Length} bytes, Border: {builtinSprite.border})");
                }
                else
                {
                    Debug.LogWarning($"找不到内置Sprite: {path}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"导出Sprite失败 {name}: {e.Message}\n{e.StackTrace}");
            }
        }

        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("导出完成", 
            $"成功导出 {exportedCount} 个UI图片到:\n{outputPath}", "确定");
    }

    static Texture2D GetTextureFromSprite(Sprite sprite)
    {
        // 获取原始纹理
        Texture2D originalTexture = sprite.texture;
        
        // 如果原始纹理是可读的，直接复制像素
        if (originalTexture.isReadable)
        {
            return GetPixelsDirectly(sprite);
        }
        
        // 如果不可读，使用RenderTexture方法
        return GetPixelsViaRenderTexture(sprite);
    }
    
    static Texture2D GetPixelsDirectly(Sprite sprite)
    {
        Texture2D originalTexture = sprite.texture;
        Rect rect = sprite.rect;
        
        // 创建新纹理
        Texture2D newTexture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        
        // 直接复制像素（注意Unity的纹理坐标是从左下角开始的）
        Color[] pixels = originalTexture.GetPixels(
            (int)rect.x, 
            (int)rect.y, 
            (int)rect.width, 
            (int)rect.height);
        
        newTexture.SetPixels(pixels);
        newTexture.Apply();
        
        Debug.Log($"直接复制像素: {sprite.name}, 区域: {rect}, 纹理大小: {originalTexture.width}x{originalTexture.height}");
        return newTexture;
    }
    
    static Texture2D GetPixelsViaRenderTexture(Sprite sprite)
    {
        Texture2D originalTexture = sprite.texture;
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D newTexture = null;
        
        try
        {
            int width = (int)sprite.rect.width;
            int height = (int)sprite.rect.height;
            
            // 创建RenderTexture
            renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture.active = renderTexture;
            
            // 清除RenderTexture
            GL.Clear(true, true, Color.clear);
            
            // 使用更精确的方法绘制Sprite区域
            Material blitMaterial = GetBlitMaterial(sprite);
            
            // 设置正确的投影矩阵
            GL.PushMatrix();
            GL.LoadOrtho();
            
            // 绘制四边形，映射整个Sprite区域到RenderTexture
            blitMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            
            // 计算UV坐标
            Rect uvRect = GetSpriteUVRect(sprite);
            
            // 左下角
            GL.TexCoord2(uvRect.x, uvRect.y);
            GL.Vertex3(0, 0, 0);
            
            // 右下角
            GL.TexCoord2(uvRect.x + uvRect.width, uvRect.y);
            GL.Vertex3(1, 0, 0);
            
            // 右上角
            GL.TexCoord2(uvRect.x + uvRect.width, uvRect.y + uvRect.height);
            GL.Vertex3(1, 1, 0);
            
            // 左上角
            GL.TexCoord2(uvRect.x, uvRect.y + uvRect.height);
            GL.Vertex3(0, 1, 0);
            
            GL.End();
            GL.PopMatrix();
            
            // 创建新纹理并读取像素
            newTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            newTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            newTexture.Apply();
            
            Debug.Log($"通过RenderTexture复制: {sprite.name}, 区域: {sprite.rect}, UV: {uvRect}");
            
            // 清理材质
            Object.DestroyImmediate(blitMaterial);
            
            return newTexture;
        }
        finally
        {
            // 恢复RenderTexture状态
            RenderTexture.active = previousActive;
            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);
        }
    }
    
    static Rect GetSpriteUVRect(Sprite sprite)
    {
        Texture2D texture = sprite.texture;
        Rect spriteRect = sprite.rect;
        
        return new Rect(
            spriteRect.x / texture.width,
            spriteRect.y / texture.height,
            spriteRect.width / texture.width,
            spriteRect.height / texture.height
        );
    }
    
    static Material GetBlitMaterial(Sprite sprite)
    {
        // 尝试使用最适合的shader
        Shader shader = Shader.Find("Hidden/BlitCopy");
        if (shader == null || !shader.isSupported)
        {
            shader = Shader.Find("Unlit/Transparent");
        }
        if (shader == null || !shader.isSupported)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null || !shader.isSupported)
        {
            // 最后的备用方案
            shader = Shader.Find("Hidden/Internal-Colored");
        }
        
        if (shader == null)
        {
            Debug.LogError("找不到合适的shader来处理Sprite纹理");
            return null;
        }
        
        Material material = new Material(shader);
        material.mainTexture = sprite.texture;
        
        Debug.Log($"使用shader: {shader.name} 处理Sprite: {sprite.name}");
        return material;
    }
    
    /// <summary>
    /// 设置Sprite的九宫格信息
    /// </summary>
    /// <param name="assetPath">资源路径</param>
    /// <param name="border">九宫格边框信息</param>
    static void SetSpriteBorder(string assetPath, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            // 设置为Sprite模式
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            
            // 设置九宫格信息
            importer.spriteBorder = border;
            
            // 设置其他常用的Sprite设置
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            
            // 重新导入资源使设置生效
            importer.SaveAndReimport();
            
            Debug.Log($"设置九宫格信息完成: {System.IO.Path.GetFileName(assetPath)} - Border: {border}");
        }
        else
        {
            Debug.LogError($"无法获取TextureImporter: {assetPath}");
        }
    }


}

public class BuiltinSpriteReplacer
{
    private const string PREFABS_PATH = "Assets/GameAsset/Sprites/Prefabs";
    private const string REPLACEMENT_SPRITES_PATH = "Assets/Arts/UI/Atlas/Common/buildinSprite";

    // 内置Sprite路径到文件名的映射
    private static readonly Dictionary<string, string> BuiltinSpriteMap = new Dictionary<string, string>()
    {
        { "UI/Skin/UISprite.psd", "UISprite.png" },
        { "UI/Skin/Background.psd", "Background.png" },
        { "UI/Skin/InputFieldBackground.psd", "InputFieldBackground.png" },
        { "UI/Skin/Knob.psd", "Knob.png" },
        { "UI/Skin/Checkmark.psd", "Checkmark.png" },
        { "UI/Skin/DropdownArrow.psd", "DropdownArrow.png" },
        { "UI/Skin/UIMask.psd", "UIMask.png" }
    };

    [MenuItem("Tools/UI/Replace Builtin UI Sprites in Prefabs")]
    static void ReplaceBuiltinSprites()
    {
        if (!EditorUtility.DisplayDialog("批量替换内置Sprite",
            $"确定要替换路径 '{PREFABS_PATH}' 下所有预制体中的内置UI Sprite吗？\n\n此操作不可撤销，建议先备份项目。",
            "确定", "取消"))
        {
            return;
        }

        // 获取所有预制体文件
        var prefabFiles = GetAllPrefabFiles(PREFABS_PATH);
        var builtinSprites = GetBuiltinSprites();
        var replacementSprites = GetReplacementSprites();

        int processedPrefabs = 0;
        int totalReplacements = 0;
        var modifiedPrefabs = new List<string>();

        try
        {
            for (int i = 0; i < prefabFiles.Count; i++)
            {
                string prefabPath = prefabFiles[i];
                EditorUtility.DisplayProgressBar("替换内置Sprite",
                    $"处理: {Path.GetFileName(prefabPath)}", (float)i / prefabFiles.Count);

                int replacements = ReplacePrefabBuiltinSprites(prefabPath, builtinSprites, replacementSprites);
                if (replacements > 0)
                {
                    totalReplacements += replacements;
                    modifiedPrefabs.Add(prefabPath);
                }

                processedPrefabs++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 显示结果
        string resultMessage = $"替换完成！\n\n" +
                             $"处理的预制体: {processedPrefabs} 个\n" +
                             $"总共替换: {totalReplacements} 个内置Sprite\n" +
                             $"修改的预制体: {modifiedPrefabs.Count} 个";

        Debug.Log("=== 内置Sprite替换结果 ===");
        Debug.Log(resultMessage);
        foreach (string modifiedPrefab in modifiedPrefabs)
        {
            Debug.Log($"修改: {modifiedPrefab}");
        }
        Debug.Log("====================");

        EditorUtility.DisplayDialog("替换完成", resultMessage, "确定");
    }

    static List<string> GetAllPrefabFiles(string path)
    {
        var prefabFiles = new List<string>();

        if (Directory.Exists(path))
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                prefabFiles.Add(assetPath);
            }
        }
        else
        {
            Debug.LogError($"预制体路径不存在: {path}");
        }

        return prefabFiles;
    }

    static Dictionary<Sprite, string> GetBuiltinSprites()
    {
        var builtinSprites = new Dictionary<Sprite, string>();

        foreach (var kvp in BuiltinSpriteMap)
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(kvp.Key);
            if (sprite != null)
            {
                builtinSprites[sprite] = kvp.Key;
            }
        }

        return builtinSprites;
    }

    static Dictionary<string, Sprite> GetReplacementSprites()
    {
        var replacementSprites = new Dictionary<string, Sprite>();

        foreach (var kvp in BuiltinSpriteMap)
        {
            string spritePath = Path.Combine(REPLACEMENT_SPRITES_PATH, kvp.Value);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
            {
                replacementSprites[kvp.Key] = sprite;
            }
            else
            {
                Debug.LogError($"找不到替换Sprite: {spritePath}");
            }
        }

        return replacementSprites;
    }

    static int ReplacePrefabBuiltinSprites(string prefabPath, Dictionary<Sprite, string> builtinSprites, Dictionary<string, Sprite> replacementSprites)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return 0;

        int replacements = 0;
        bool prefabModified = false;

        Image[] images = prefab.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.sprite != null && builtinSprites.ContainsKey(image.sprite))
            {
                string builtinSpritePath = builtinSprites[image.sprite];
                if (replacementSprites.ContainsKey(builtinSpritePath))
                {
                    Sprite replacementSprite = replacementSprites[builtinSpritePath];

                    // 记录撤销操作
                    Undo.RecordObject(image, $"Replace builtin sprite in {prefabPath}");
                    image.sprite = replacementSprite;

                    EditorUtility.SetDirty(image);
                    prefabModified = true;
                    replacements++;

                    Debug.Log($"替换: {prefabPath} -> {GetGameObjectPath(image.transform, prefab.transform)} -> {builtinSpritePath}");
                }
                else
                {
                    Debug.LogWarning($"找不到替换的Sprite: {builtinSpritePath}");
                }
            }
        }

        if (prefabModified)
        {
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssetIfDirty(prefab);
        }

        return replacements;
    }

    static string GetGameObjectPath(Transform transform, Transform root)
    {
        if (transform == root)
            return root.name;

        return GetGameObjectPath(transform.parent, root) + "/" + transform.name;
    }
}
