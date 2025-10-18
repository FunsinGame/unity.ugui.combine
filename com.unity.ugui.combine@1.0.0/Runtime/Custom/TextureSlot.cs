using UnityEngine.Pool;

namespace UnityEngine.UI.CombineRender
{
    internal sealed class TextureSlot
    {
        internal static readonly ObjectPool<TextureSlot> pool = new(
            () => new TextureSlot(),
            null,
            (item) =>
            {
                item?.Reset();
            },
            null,
            true,
            MaterialEntry.MAX_SLOT_COUNT,
            MaterialEntry.MAX_SLOT_COUNT * 4
        );

        public Texture texture;
        public int refCount = 0;

        public void Reset()
        {
            texture = null;
            refCount = 0;
        }
    }
}
