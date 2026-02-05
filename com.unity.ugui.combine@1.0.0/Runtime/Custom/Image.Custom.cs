using System;

namespace UnityEngine.UI
{
    public partial class Image
    {
        public uint handleID = 0;

        public Action<uint> disposeHandleFunc;

        internal override CombineType CombineType => CombineType.Image;

        protected override void OnDestroy()
        {
            if (handleID != 0)
            {
                disposeHandleFunc?.Invoke(handleID);
                handleID = 0;
            }
            base.OnDestroy();
        }
    }
}