using Dalamud.Plugin;
using ECommons.DalamudServices;
using System;

namespace ToshiBox.IPC
{
    public static class PandoraIPC
    {
        private static Func<string, bool?>? GetFeatureEnabled;
        private static Action<string, bool>? SetFeatureEnabled;
        private static Func<string, string, bool?>? GetConfigEnabled;
        private static Action<string, string, bool>? SetConfigEnabled;
        private static Action<string, int>? PauseFeature;

        public static void Init()
        {
            try
            {
                var pi = Svc.PluginInterface;
                GetFeatureEnabled = pi.GetIpcSubscriber<string, bool?>("PandorasBox.GetFeatureEnabled").InvokeFunc;
                SetFeatureEnabled = pi.GetIpcSubscriber<string, bool, object>("PandorasBox.SetFeatureEnabled").InvokeAction;
                GetConfigEnabled = pi.GetIpcSubscriber<string, string, bool?>("PandorasBox.GetConfigEnabled").InvokeFunc;
                SetConfigEnabled = pi.GetIpcSubscriber<string, string, bool, object>("PandorasBox.SetConfigEnabled").InvokeAction;
                PauseFeature     = pi.GetIpcSubscriber<string, int, object>("PandorasBox.PauseFeature").InvokeAction;
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"[ToshiBox] Failed to initialize PandoraBox IPC: {ex}");
            }
        }

        public static void Dispose()
        {
            GetFeatureEnabled = null;
            SetFeatureEnabled = null;
            GetConfigEnabled = null;
            SetConfigEnabled = null;
            PauseFeature = null;
        }

        public static void DisableFeature(string name)
        {
            SetFeatureEnabled?.Invoke(name, false);
        }

        public static void EnableFeature(string name)
        {
            SetFeatureEnabled?.Invoke(name, true);
        }

        public static bool? IsFeatureEnabled(string name)
        {
            return GetFeatureEnabled?.Invoke(name);
        }

        public static bool? IsConfigEnabled(string featureName, string configName)
        {
            return GetConfigEnabled?.Invoke(featureName, configName);
        }

        public static void SetConfig(string featureName, string configName, bool state)
        {
            SetConfigEnabled?.Invoke(featureName, configName, state);
        }

        public static void Pause(string featureName, int milliseconds)
        {
            PauseFeature?.Invoke(featureName, milliseconds);
        }
    }
}
