using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;

namespace ServerRoomBackground.Prepatch
{
    /// <summary>
    /// Adds EEnvironmentUIType.ServerRoomEnvironmentUiType (= 11) to the client's Assembly-CSharp before it loads, the same
    /// way WTT-ContentBackport adds its backgrounds (6..10): the settings dropdown lists Enum values, so a new background
    /// needs a real enum member.
    /// </summary>
    public static class Patcher
    {
        public const string ValueName = "ServerRoomEnvironmentUiType";
        public const int Value = 11;

        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        public static void Patch(ref AssemblyDefinition assembly)
        {
            var log = Logger.CreateLogSource("ServerRoomBackground.Prepatch");
            try
            {
                var plugin = Path.Combine(BepInEx.Paths.PluginPath, "ServerRoomBackground", "ServerRoomBackground.dll");
                if (!File.Exists(plugin)) { log.LogWarning("[ServerRoomBackground] plugin missing (" + plugin + "), enum not patched"); return; }
                var type = assembly.MainModule.GetType("EFT.EEnvironmentUIType");
                if (type == null) { log.LogError("[ServerRoomBackground] EFT.EEnvironmentUIType not found"); return; }
                if (type.Fields.Any(f => f.Name == ValueName)) { log.LogInfo("[ServerRoomBackground] enum value already present"); return; }
                var clash = type.Fields.FirstOrDefault(f => f.HasConstant && Convert.ToInt32(f.Constant) == Value);
                if (clash != null) { log.LogError($"[ServerRoomBackground] enum value {Value} already used by {clash.Name}: not patched"); return; }
                type.Fields.Add(new FieldDefinition(ValueName,
                    FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, type) { Constant = Value });
                log.LogInfo($"[ServerRoomBackground] added EEnvironmentUIType.{ValueName} = {Value} (members now: {string.Join(", ", type.Fields.Where(f => f.HasConstant).Select(f => f.Name + "=" + f.Constant))})");
            }
            catch (Exception e)
            {
                log.LogError("[ServerRoomBackground] enum patch failed: " + e);
            }
        }
    }
}
