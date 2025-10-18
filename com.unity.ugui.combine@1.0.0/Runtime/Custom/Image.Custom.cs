using System;

namespace UnityEngine.UI
{
    public partial class Image
    {
        public static Action<uint> ReleaseSpriteFunc;
        public static Action<Image> RegisterImageFunc;
        public static Action<Image> UnregisterImageFunc;
        private Sprite _customSprite = null;
        private uint _customSpriteGuid = 0;

        public uint CustomSpriteGuid => _customSpriteGuid;


        internal override CombineType CombineType => CombineType.Image;

        protected override void OnDestroy()
        {
            if (_customSpriteGuid > 0)
            {
                ReleaseSpriteFunc?.Invoke(_customSpriteGuid);
                UnregisterImageFunc?.Invoke(this);
                _customSprite = null;
                _customSpriteGuid = 0;
            }

            base.OnDestroy();
        }

        public void SetSpriteEx(Sprite sprite, uint guid)
        {
            if (_customSpriteGuid > 0)
            {
                ReleaseSpriteFunc?.Invoke(_customSpriteGuid);
                _customSprite = null;
                _customSpriteGuid = 0;
            }

            _customSprite = sprite;
            _customSpriteGuid = guid;
            this.sprite = sprite;

            if (!isActiveAndEnabled)
            {
                if (_customSprite == null)
                {
                    UnregisterImageFunc?.Invoke(this);
                }
                else
                {
                    RegisterImageFunc?.Invoke(this);
                }
            }
        }
    }
}