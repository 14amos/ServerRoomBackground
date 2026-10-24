using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace ServerRoomBackground.Server
{
    public record ModMetadata : IModMetadata
    {
        public string ModGuid { get; init; } = "com.amos.serverroombackground";
        public string Name { get; init; } = "Server Room Background";
        public string Author { get; init; } = "amos";
        public List<string> Contributors { get; init; }
        public SemanticVersioning.Version Version { get; init; } = new SemanticVersioning.Version(1, 0, 0, null, null);
        public SemanticVersioning.Range SptVersion { get; init; } = new SemanticVersioning.Range("~4.1", false);
        public bool HasPrepatcher { get; init; }
        public List<string> Incompatibilities { get; init; }
        public Dictionary<string, SemanticVersioning.Range> ModDependencies { get; init; }
        public string Url { get; init; }
        public string License { get; init; } = "MIT";
    }

    /// <summary>
    /// The background as a customization item under EnvironmentUI (67584ea0ff58ff0e7909e435), shaped like the vanilla
    /// Cyber entry, unlocked for every profile (CustomisationStorage, like the 4 default backgrounds) and named in every
    /// locale. Runs right after WTT-ContentBackport (Preload + 2) and before UnlockAllBackgrounds (PostLoad).
    /// </summary>
    [Injectable(TypePriority = OnLoadOrder.Preload + 3)]
    public class ServerRoomBackgroundServer(TemplateTable templates, LocaleTable locales, ISptLogger<ServerRoomBackgroundServer> logger) : IOnLoad
    {
        public const string Id = "6a5e7a11c0ffee00000b0d11";
        const string EnvironmentNode = "67584ea0ff58ff0e7909e435";
        const string TypeName = "ServerRoomEnvironmentUiType";
        const string DisplayName = "Server Room";

        public Task OnLoadAsync(CancellationToken cancellationToken)
        {
            var id = new MongoId(Id);
            if (!templates.Customization.ContainsKey(id))
            {
                templates.Customization[id] = new CustomizationItem
                {
                    Id = id,
                    Name = "ServerRoomEnvironmentUI",
                    Parent = EnvironmentNode,
                    Type = "Item",
                    Prototype = null,
                    Properties = new CustomizationProperties
                    {
                        AvailableAsDefault = false,
                        Description = "ServerRoomEnvironmentUI",
                        Name = "ServerRoomEnvironmentUI",
                        ShortName = "ServerRoomEnvironmentUI",
                        ProfileVersions = new List<string>(),
                        Side = new List<string> { "Savage", "Bear", "Usec" },
                        EnvironmentUIType = TypeName,
                    },
                };
            }
            if (!templates.CustomisationStorage.Any(c => c.Id == id))
                templates.CustomisationStorage.Add(new CustomisationStorage { Id = id, Source = "default", Type = "environment" });
            foreach (var (lang, lazy) in locales.Global)
                lazy.AddTransformer(d => { if (d != null) d[TypeName] = DisplayName; return d; });
            logger.Success($"[ServerRoomBackground] background {TypeName} ({Id}) added and unlocked, name \"{DisplayName}\" in {locales.Global.Count} locales");
            return Task.CompletedTask;
        }
    }
}
