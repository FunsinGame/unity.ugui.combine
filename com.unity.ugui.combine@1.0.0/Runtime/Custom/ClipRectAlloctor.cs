using System;
using System.Collections.Generic;
using System.Text;

namespace UnityEngine.UI.CombineRender
{
    internal class ClipRectAlloctor
    {
        // !需要同时修改shader的数组长度
        private const int MAX_CLIPRECT_COUNT = 256;

        private static readonly int ID_ClipRects = Shader.PropertyToID("_ClipRectArray");
        private static readonly int ID_Softness = Shader.PropertyToID("_SoftnessArray");

        private Dictionary<RectMask2D, (int hash, int index)> _maskMap = new();
        private Vector4[] _clipRectArray;
        private Vector4[] _softnessArray;
        private List<int> _freeIndexs = new();
        private bool _logFlag = false;
        private bool _isDirty = false;
        private bool _canWriteDetailLog = true;

        public ClipRectAlloctor()
        {
            _clipRectArray = new Vector4[MAX_CLIPRECT_COUNT];
            _softnessArray = new Vector4[MAX_CLIPRECT_COUNT];

            // 默认0为无裁剪
            _clipRectArray[0] = (
                new Vector4(
                    -float.PositiveInfinity,
                    -float.PositiveInfinity,
                    float.PositiveInfinity,
                    float.PositiveInfinity
                )
            );
            _softnessArray[0] = (Vector2.zero);

            for (int i = _clipRectArray.Length - 1; i >= 1; i--)
            {
                _freeIndexs.Add(i);
            }

            Shader.SetGlobalVectorArray(ID_ClipRects, _clipRectArray);
            Shader.SetGlobalVectorArray(ID_Softness, _softnessArray);
        }

        internal int GetClipRectIndex(RectMask2D mask, Rect clipRect, Vector2Int softness)
        {
            if (clipRect.Equals(Rect.zero))
            {
                return 0;
            }

            if (!_maskMap.TryGetValue(mask, out var info))
            {
                // 分配一个槽位
                if (_freeIndexs.Count > 0)
                {
                    info.index = _freeIndexs[_freeIndexs.Count - 1];
                    _freeIndexs.RemoveAt(_freeIndexs.Count - 1);
                    _logFlag = false;
                }
                // 没有可用槽位
                else
                {
                    DebugErrorLog(mask);
                    return 0;
                }

                _maskMap[mask] = info;
            }

            int x = (int)(clipRect.x * 100);
            int y = (int)(clipRect.y * 100);
            int width = (int)(clipRect.width * 100);
            int height = (int)(clipRect.height * 100);
            int sx = softness.x;
            int sy = softness.y;

            int hash = x;
            hash = (hash * 397) ^ y;
            hash = (hash * 397) ^ width;
            hash = (hash * 397) ^ height;
            hash = (hash * 397) ^ sx;
            hash = (hash * 397) ^ sy;

            if (info.hash != hash)
            {
                _clipRectArray[info.index] = new Vector4(
                    x / 100f,
                    y / 100f,
                    (x + width) / 100f,
                    (y + height) / 100f
                );
                _softnessArray[info.index] = new Vector2(sx, sy);
                _isDirty = true;

                info.hash = hash;
                _maskMap[mask] = info;
            }

            return info.index;
        }

        private void DebugErrorLog(RectMask2D mask)
        {
            // 避免每帧日志输出
            if (!_logFlag)
            {
                // 详细日志仅输出一次
                if (_canWriteDetailLog)
                {
                    _canWriteDetailLog = false;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"同时存在的RectMask2D已超出最大限制({MAX_CLIPRECT_COUNT}个)");
                    foreach (var kv in _maskMap)
                    {
                        if (kv.Key == null)
                        {
                            sb.AppendLine($"index = {kv.Value.index}, mask = null");
                        }
                        else
                        {
                            sb.AppendLine($"index = {kv.Value.index}, mask = {CombineRenderManager.GetComponentPath(kv.Key.transform)}");
                        }
                    }
                    Debug.LogError(sb.ToString());
                }
                else
                {
                    string path = CombineRenderManager.GetComponentPath(mask?.transform);
                    Debug.LogError(
                        $"同时存在的RectMask2D已超出最大限制({MAX_CLIPRECT_COUNT}个), path = {path}"
                    );
                }
                _logFlag = true;
            }
        }

        internal void FreeClipRect(RectMask2D mask)
        {
            if (mask != null && _maskMap.TryGetValue(mask, out var info))
            {
                if (info.index > 0)
                {
                    _freeIndexs.Add(info.index);
                }

                _maskMap.Remove(mask);
            }
        }

        internal void OnPreRender()
        {
            if (_isDirty)
            {
                _isDirty = false;
                Shader.SetGlobalVectorArray(ID_ClipRects, _clipRectArray);
                Shader.SetGlobalVectorArray(ID_Softness, _softnessArray);
            }
        }
    }
}
