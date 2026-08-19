using Dusk;

namespace TheEar
{
    internal class EarContentHandler : ContentHandler<EarContentHandler>
    {
        internal EarAssets? earAssets;

        public class EarAssets(DuskMod mod, string filePath) : AssetBundleLoader<EarAssets>(mod, filePath) { }
        public EarContentHandler(DuskMod mod) : base(mod)
        {
            RegisterContent("theear", out earAssets);
        }
    }
}
