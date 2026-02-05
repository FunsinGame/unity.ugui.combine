using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 基于 Sprite 轮廓数据的多边形图片组件，用于精确检测点击区域
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Polygon Image", 21)]
public class PolygonImage : Image
{
    // 缓存的轮廓数据
    private List<List<Vector2>> _cachedOutlines = new List<List<Vector2>>();
    private bool _outlinesCached = false;

    // 是否启用多边形检测（如果为 false，则使用默认的矩形检测）
    [Header("启用多边形检测后，Sprite Model必须设置为Polygon模式\n并且编辑Sprite的Custom Physics Shape轮廓用于范围检测")]
    [SerializeField]
    private bool _usePolygonHitTest = true;
    
    /// <summary>
    /// 是否使用多边形点击检测
    /// </summary>
    public bool usePolygonHitTest
    {
        get { return _usePolygonHitTest; }
        set
        {
            if (_usePolygonHitTest != value)
            {
                _usePolygonHitTest = value;
                if (value)
                {
                    CacheOutlines();
                }
            }
        }
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        CacheOutlines();
    }
    
    /// <summary>
    /// 当 Sprite 改变时，重新缓存轮廓数据
    /// </summary>
    public override void SetVerticesDirty()
    {
        base.SetVerticesDirty();
        _outlinesCached = false;
    }
    
    /// <summary>
    /// 缓存 Sprite 的轮廓数据
    /// </summary>
    private void CacheOutlines()
    {
        _cachedOutlines.Clear();
        _outlinesCached = false;
        
        if (sprite == null || !_usePolygonHitTest)
        {
            return;
        }
        
        // 使用 Unity 的 GetPhysicsShape 方法获取轮廓
        int shapeCount = sprite.GetPhysicsShapeCount();
        
        if (shapeCount == 0)
        {
            // 如果没有物理形状，尝试使用 Sprite 的 vertices（如果可用）
            if (sprite.vertices != null && sprite.vertices.Length > 0)
            {
                // 从 vertices 提取轮廓（简化处理，实际可能需要更复杂的算法）
                List<Vector2> outline = new List<Vector2>();
                foreach (var vertex in sprite.vertices)
                {
                    outline.Add(vertex);
                }
                if (outline.Count > 0)
                {
                    _cachedOutlines.Add(outline);
                }
            }
        }
        else
        {
            // 获取所有物理形状的轮廓
            for (int i = 0; i < shapeCount; i++)
            {
                int pointCount = sprite.GetPhysicsShapePointCount(i);
                if (pointCount > 0)
                {
                    List<Vector2> points = new List<Vector2>(pointCount);
                    sprite.GetPhysicsShape(i, points);
                    if (points.Count > 0)
                    {
                        _cachedOutlines.Add(points);
                    }
                }
            }
        }
        
        _outlinesCached = true;
    }
    
    /// <summary>
    /// 重写射线检测方法，使用多边形轮廓进行精确检测
    /// </summary>
    public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // 如果不使用多边形检测，或者没有设置 alphaHitTestMinimumThreshold，使用默认行为
        if (!_usePolygonHitTest)
        {
            return base.IsRaycastLocationValid(screenPoint, eventCamera);
        }
        
        // 如果没有 Sprite，使用默认行为
        if (activeSprite == null)
        {
            return true;
        }
        
        // 确保轮廓数据已缓存
        if (!_outlinesCached)
        {
            CacheOutlines();
        }
        
        // 如果没有轮廓数据，回退到 alpha 检测
        if (_cachedOutlines.Count == 0)
        {
            return base.IsRaycastLocationValid(screenPoint, eventCamera);
        }
        
        // 将屏幕坐标转换为本地坐标
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out local))
        {
            return false;
        }
        
        Rect rect = GetPixelAdjustedRect();
        
        if (preserveAspect)
        {
            PreserveSpriteAspectRatio(ref rect, new Vector2(activeSprite.texture.width, activeSprite.texture.height));
        }
        
        // 转换为以左下角为原点的坐标
        local.x += rectTransform.pivot.x * rect.width;
        local.y += rectTransform.pivot.y * rect.height;
        
        // 将本地坐标映射到 Sprite 空间
        Vector2 spriteLocal = MapCoordinateToSpriteSpace(local, rect);
        
        // 检查点是否在任何轮廓多边形内部
        foreach (var outline in _cachedOutlines)
        {
            if (IsPointInPolygon(spriteLocal, outline))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// 将本地坐标映射到 Sprite 空间坐标（Unity 单位，相对于 Sprite pivot）
    /// </summary>
    private Vector2 MapCoordinateToSpriteSpace(Vector2 local, Rect rect)
    {
        Rect spriteRect = activeSprite.rect;
        Vector2 spritePivot = activeSprite.pivot;

        // 根据 Image 类型进行坐标映射（得到像素空间的坐标）
        Vector2 pixelCoord;

        if (type == Type.Simple || type == Type.Filled)
        {
            pixelCoord = new Vector2(
                spriteRect.position.x + local.x * spriteRect.width / rect.width,
                spriteRect.position.y + local.y * spriteRect.height / rect.height
            );
        }
        else
        {
            // 对于 Sliced 和 Tiled 类型，使用与基类相同的映射逻辑
            Vector4 border = activeSprite.border;
            Vector4 adjustedBorder = GetAdjustedBorders(border / pixelsPerUnit, rect);

            for (int i = 0; i < 2; i++)
            {
                if (local[i] <= adjustedBorder[i])
                    continue;

                if (rect.size[i] - local[i] <= adjustedBorder[i + 2])
                {
                    local[i] -= (rect.size[i] - spriteRect.size[i]);
                    continue;
                }

                if (type == Type.Sliced)
                {
                    float lerp = Mathf.InverseLerp(adjustedBorder[i], rect.size[i] - adjustedBorder[i + 2], local[i]);
                    local[i] = Mathf.Lerp(border[i], spriteRect.size[i] - border[i + 2], lerp);
                }
                else
                {
                    local[i] -= adjustedBorder[i];
                    local[i] = Mathf.Repeat(local[i], spriteRect.size[i] - border[i] - border[i + 2]);
                    local[i] += border[i];
                }
            }

            pixelCoord = local + spriteRect.position;
        }

        // 将像素坐标转换为 Unity 单位，并相对于 Sprite pivot 进行偏移
        // GetPhysicsShape 返回的坐标是相对于 pivot 的 Unity 单位坐标
        float spritePixelsPerUnit = activeSprite.pixelsPerUnit;
        Vector2 unityCoord = (pixelCoord - spritePivot) / spritePixelsPerUnit;

        return unityCoord;
    }

    /// <summary>
    /// 使用射线法判断点是否在多边形内部
    /// </summary>
    private bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }
        
        int intersections = 0;
        int vertexCount = polygon.Count;
        
        for (int i = 0; i < vertexCount; i++)
        {
            Vector2 p1 = polygon[i];
            Vector2 p2 = polygon[(i + 1) % vertexCount];
            
            // 检查射线是否与边相交
            if (((p1.y > point.y) != (p2.y > point.y)) &&
                (point.x < (p2.x - p1.x) * (point.y - p1.y) / (p2.y - p1.y) + p1.x))
            {
                intersections++;
            }
        }
        
        // 奇数个交点表示点在多边形内部
        return (intersections % 2) == 1;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 当 Sprite 改变时重新缓存轮廓
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        if (Application.isPlaying)
        {
            _outlinesCached = false;
        }
    }
#endif
}