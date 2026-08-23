using Dusk;
using UnityEngine;

namespace TheEar
{
    internal class EarContentHandler : ContentHandler<EarContentHandler>
    {
        internal EarAssets? earAssets;

        public class EarAssets(DuskMod mod, string filePath) : AssetBundleLoader<EarAssets>(mod, filePath)
        {
        [LoadFromBundle("TheEar.prefab")]
        public GameObject TheEar { get; private set; } = null!;
        }
        public EarContentHandler(DuskMod mod) : base(mod)
        {
            RegisterContent("theear", out earAssets);
        }
    }
}
