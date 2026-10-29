using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using RoR2;

namespace ItemRoulettePatcher
{
    /// <summary>
    /// Everything that touches ItemRoulette goes through here, by name, via reflection.
    /// The patcher never has a compile-time reference to ItemRoulette.dll.
    /// </summary>
    internal static class ItemRouletteBridge
    {
        public const string PluginGuid = "com.user.itemroulette";

        /// <summary>The ItemRoulette version these patches were written and tested against.</summary>
        public const string TestedVersion = "1.4.4";

        private static Assembly _assembly;
        private static PropertyInfo _isRussianProperty;

        public static bool Init()
        {
            if (Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info) && info.Instance != null)
            {
                _assembly = info.Instance.GetType().Assembly;
                var version = info.Metadata.Version.ToString();
                if (version != TestedVersion)
                {
                    Plugin.Log.LogWarning($"ItemRoulette {version} is installed but these patches were written against {TestedVersion}. " +
                                          "Each patch checks that its target still exists and is skipped (with a warning) if not.");
                }
                return true;
            }

            return false;
        }

        public static Type FindType(string fullName)
        {
            var type = _assembly?.GetType(fullName, false);
            if (type == null)
            {
                Plugin.Log.LogWarning($"ItemRoulette type not found: {fullName}");
            }

            return type;
        }

        /// <summary>Finds a method by name. Pass <paramref name="parameterTypes"/> when the name is overloaded.</summary>
        public static MethodBase FindMethod(string typeName, string methodName, Type[] parameterTypes = null)
        {
            var type = FindType(typeName);
            if (type == null)
            {
                return null;
            }

            try
            {
                var method = parameterTypes == null
                    ? AccessTools.Method(type, methodName)
                    : AccessTools.Method(type, methodName, parameterTypes);
                if (method == null)
                {
                    Plugin.Log.LogWarning($"ItemRoulette method not found: {typeName}.{methodName}");
                }

                return method;
            }
            catch (AmbiguousMatchException)
            {
                Plugin.Log.LogWarning($"ItemRoulette method is overloaded, parameter types required: {typeName}.{methodName}");
                return null;
            }
        }

        /// <summary>
        /// ItemRoulette's own "is the UI in Russian" decision, so translated text always agrees with the
        /// language the rest of the mod resolved (config setting, then game language when set to Auto).
        /// </summary>
        public static bool IsRussian()
        {
            try
            {
                _isRussianProperty ??= AccessTools.Property(FindType("ItemRoulette.Core.Localization"), "IsRussian");
                if (_isRussianProperty != null)
                {
                    return (bool)_isRussianProperty.GetValue(null);
                }
            }
            catch (Exception)
            {
                // fall through to the game-language check
            }

            return Language.currentLanguageName != null &&
                   Language.currentLanguageName.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        }
    }
}
