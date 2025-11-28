using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;

using ResoniteDiscordRpc;

using Elements.Core;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FrooxEngine.Interfacing {
    public class DiscordRpcConnector : IPlatformConnector, IDisposable {
        public readonly string LARGE_IMAGE_ID = "resonite-logo";

        public readonly string SMALL_IMAGE_ID = "resonite-logo";

        private DiscordRpcClient discord;

        public PlatformInterface Interface { get; private set; }

        public int Priority => -10;

        public string PlatformName => "Discord";

        public string Username { get; private set; }

        public string PlatformUserId { get; private set; }

        public bool IsPlatformNameUnique => false;

        public static bool Initialized { get; private set; }

        public static bool ShouldUpdate { get; private set; }

        public RichPresenceLevel RichPresencePreference { get; private set; } = RichPresenceLevel.Full;

        public bool IsRichPresenceEnabled => RichPresencePreference > RichPresenceLevel.None;

        public Task<bool> Initialize(PlatformInterface platformInterface) {
            Interface = platformInterface;
            discord = new DiscordRpcClient(Interface.Engine.PlatformProfile.DiscordAppId.ToString());
            discord.Logger = new DiscordRpcLogger();
            discord.OnReady += UserCallback;
            if (!discord.Initialize()) {
                Mod.Error("Could not initialize Discord RPC connector");
                discord.Dispose();
                return Task.FromResult(false);
            }

            UpdatePresence(presence => {
                presence.Type = ActivityType.Playing;
                presence.StatusDisplay = StatusDisplayType.Name;
                presence.Assets = new() {
                    LargeImageKey = LARGE_IMAGE_ID,
                };
            });

            Initialized = true;
            ShouldUpdate = true;
            Mod.Msg("Discord connector initialized.");
            Settings.RegisterValueChanges<DiscordIntegrationSettings>(OnRichPresenceSettingsChanged);
            return Task.FromResult(true);
        }

        private void OnRichPresenceSettingsChanged(DiscordIntegrationSettings setting) {
            if (setting.RichPresence.Value < RichPresenceLevel.Basic) {
                ClearCurrentStatus();
            }

            RichPresencePreference = setting.RichPresence.Value;
        }

        public void Update() { }

        public void Dispose() {
            discord.Dispose();
        }

        public void SetCurrentStatus(World world, bool isPrivate, int totalWorldCount) {
            if (Initialized && IsRichPresenceEnabled) {
                if (isPrivate || RichPresencePreference < RichPresenceLevel.Full) {
                    UpdatePresence(presence => SetPrivateActivity(presence, world));
                } else {
                    UpdatePresence(presence => SetPublicActivity(presence, world));
                }
            }
        }

        private void SetPrivateActivity(RichPresence presence, World world) {
            presence.Timestamps = new Timestamps(world.Time.LocalSessionBeginTime);
            presence.State = world.GetLocalized("Discord.RichPresence.InPrivateWorld");
            presence.Assets = new() {
                LargeImageKey = LARGE_IMAGE_ID,
            };
            presence.Details = null;
            presence.Party = null;
            presence.Buttons = null;
        }

        private void SetPublicActivity(RichPresence presence, World world) {
            string rawString = StringTokenizer.Tokenize(world.Name).GetRawString();
            presence.Timestamps = new Timestamps(world.Time.LocalSessionBeginTime);
            presence.State = rawString;

            int publicWorldCount = world.WorldManager.Worlds.Count(w => w.IsPublic && world.MaxUsers > 1 && !world.HideFromListing);
            presence.Details = world.GetLocalized(
                "Discord.RichPresence.PublicWorldDetails", null,
                ("worldName", world.GetLocalized("Discord.RichPresence.InPublicWorld")),
                ("totalWorlds", publicWorldCount));
            presence.Party = new Party() {
                ID = world.SessionId,
                Privacy = Party.PrivacySetting.Public,
                Size = world.UserCount,
                Max = world.MaxUsers,
            };

            string sessionInfoUrl = Interface.Engine.PlatformProfile.GetSessionWebUri(world.SessionId).ToString();

            if (Mod.Thumbnail && world.Time.LocalWorldTime >= 150) {
                // The thumbnail takes a while to be initialized
                presence.Assets = new() {
                    LargeImageKey = $"{sessionInfoUrl}/thumbnail",
                    SmallImageKey = SMALL_IMAGE_ID,
                };
                if (Mod.SessionInfo) {
                    presence.Assets.LargeImageUrl = sessionInfoUrl;
                    presence.Assets.LargeImageText = rawString;
                }
            } else {
                presence.Assets = new() {
                    LargeImageKey = LARGE_IMAGE_ID,
                };
            }

            List<Button> buttons = [];
            if (Mod.SessionInfo) {
                buttons.Add(new Button() {
                    Label = world.GetLocalized("World.Detail.SessionInformationHeader"),
                    Url = sessionInfoUrl
                });
            }
            if (Mod.Join) {
                buttons.Add(new Button() {
                    Label = world.GetLocalized("World.Actions.Join"),
                    Url = Interface.Engine.PlatformProfile.GetSessionUri(world.SessionId).ToString()
                });
            }
            presence.Buttons = buttons.ToArray();
        }

        private void SetBlankActivity() {
            if (Initialized) {
                UpdatePresence(presence => {
                    presence.Timestamps = null;
                    presence.State = null;
                    presence.Assets = new() {
                        LargeImageKey = LARGE_IMAGE_ID,
                    };
                    presence.Details = null;
                    presence.Party = null;
                    presence.Buttons = null;
                });
            }
        }

        private void UpdatePresence(Action<RichPresence> action) {
            var response = discord.Update(action);
            if (Mod.IsDebugEnabled()) {
                Mod.Debug($"Updated Discord presence: {JsonConvert.SerializeObject(response)}");
            }
        }

        public void ClearCurrentStatus() {
            SetBlankActivity();
        }


        public void UserCallback(object sender, ReadyMessage args) {
            Username = args.User.ToString();
            PlatformUserId = args.User.ID.ToString(CultureInfo.InvariantCulture);
        }

        public void NotifyOfFile(string file, string name) {
        }

        public void NotifyOfScreenshot(World world, string file, ScreenshotType type, DateTime timestamp) {
        }

        public void NotifyOfLocalUser(User user) {
        }
    }

    public class DiscordRpcLogger : ILogger {
        public void Trace(string message, params object[] args) {
            Mod.Debug(String.Format(message, args));
        }
        public void Info(string message, params object[] args) {
            Mod.Msg(String.Format(message, args));
        }
        public void Warning(string message, params object[] args) {
            Mod.Warn(String.Format(message, args));
        }
        public void Error(string message, params object[] args) {
            Mod.Error(String.Format(message, args));
        }
        public LogLevel Level {
            get => Mod.IsDebugEnabled() ? LogLevel.Trace : LogLevel.Info;
            set { }
        }
    }
}