using UnityEngine;

namespace ContainerWindowFinder
{
    public class ModEntry : IMod
    {
        public const string ModId = "container_window_finder";

        internal static ModLog Log;
        private GameObject host;

        public void Init(ModManifest manifest)
        {
            Log = new ModLog(manifest);
            Settings.Define(manifest.ID);
        }

        public void OnEnable()
        {
            ModHook.OnGameLoadedNormal += EnsureHost;
            EnsureHost();
        }

        public void OnDisable()
        {
            ModHook.OnGameLoadedNormal -= EnsureHost;
            if (host != null)
            {
                Object.Destroy(host);
                host = null;
            }
        }

        private void EnsureHost()
        {
            if (host != null) return;
            host = new GameObject("ContainerWindowFinder");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<WindowFinderBehaviour>();
            Log?.Log("ready");
        }
    }
}
