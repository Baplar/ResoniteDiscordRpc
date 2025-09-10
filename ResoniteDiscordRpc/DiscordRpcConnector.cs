using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;

using Elements.Core;

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
                UniLog.Error("Could not initialize Discord RPC connector");
                return Task.FromResult(false);
            }
            discord.UpdateLargeAsset(LARGE_IMAGE_ID);
            discord.UpdateType(ActivityType.Playing);
            discord.UpdateStatusDisplayType(StatusDisplayType.Name);
            Initialized = true;
            ShouldUpdate = true;
            UniLog.Log("Discord connector initialized.");
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
                    discord.Update(presence => {
                        presence.Timestamps = new(world.Time.LocalSessionBeginTime);
                        presence.State = world.GetLocalized("Discord.RichPresence.InPrivateWorld");
                        presence.StateUrl = null;
                        presence.Assets = new() {
                            LargeImageKey = LARGE_IMAGE_ID,
                            LargeImageText = world.GetLocalized("Discord.RichPresence.InPrivateLargeText", null, ("version", Engine.CurrentVersion)),
                        };
                        presence.Details = null;
                        presence.DetailsUrl = null;
                        presence.Party = null;
                        presence.Buttons = null;
                    });
                } else {
                    discord.Update(presence => {
                        presence.Timestamps = new Timestamps(world.Time.LocalSessionBeginTime);
                        presence.State = world.GetLocalized("Discord.RichPresence.InPublicWorld");
                        string goResoniteUrl = Interface.Engine.PlatformProfile.GetSessionWebUri(world.SessionId).ToString();
                        presence.StateUrl = goResoniteUrl;
                        if (world.Time.WorldTime >= 60) {
                            presence.Assets = new() {
                                LargeImageKey = $"{goResoniteUrl}/thumbnail",
                                LargeImageText = world.GetLocalized("Discord.RichPresence.InPublicLargeText", null, ("version", Engine.CurrentVersion)),
                                LargeImageUrl = goResoniteUrl,
                                SmallImageKey = SMALL_IMAGE_ID,
                            };
                        } else {
                            // The thumbnail has probably not had time to be generated yet
                            presence.Assets = new() {
                                LargeImageKey = LARGE_IMAGE_ID,
                                LargeImageText = world.GetLocalized("Discord.RichPresence.InPublicLargeText", null, ("version", Engine.CurrentVersion)),
                                LargeImageUrl = goResoniteUrl,
                            };
                        }
                        string rawString = StringTokenizer.Tokenize(world.Name).GetRawString();
                        int publicWorldCount = world.WorldManager.Worlds.Count(w => w.IsPublic && world.MaxUsers > 1 && !world.HideFromListing);
                        presence.Details = world.GetLocalized("Discord.RichPresence.PublicWorldDetails", null, ("worldName", rawString), ("totalWorlds", publicWorldCount));
                        presence.DetailsUrl = goResoniteUrl;
                        presence.Party = new Party() {
                            ID = world.SessionId,
                            Privacy = Party.PrivacySetting.Public,
                            Size = world.UserCount,
                            Max = world.MaxUsers,
                        };
                        presence.Buttons = [
                            new Button() {
                                Label = world.GetLocalized("World.Detail.SessionInformationHeader", null),
                                Url = goResoniteUrl
                            },
                            new Button() {
                                Label = world.GetLocalized("World.Actions.Join", null),
                                Url = Interface.Engine.PlatformProfile.GetSessionUri(world.SessionId).ToString()
                            },
                        ];
                    });
                }
            }
        }

        private void SetBlankActivity() {
            if (Initialized) {
                discord.Update(presence => {
                    presence.Timestamps = null;
                    presence.State = null;
                    presence.StateUrl = null;
                    presence.Assets = new() {
                        LargeImageKey = LARGE_IMAGE_ID,
                    };
                    presence.Details = null;
                    presence.DetailsUrl = null;
                    presence.Party = null;
                    presence.Buttons = null;
                });
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
            // Ignore
        }
        public void Info(string message, params object[] args) {
            UniLog.Log(String.Format($"Discord:Info - {message}", args));
        }
        public void Warning(string message, params object[] args) {
            UniLog.Warning(String.Format($"Discord:Warning - {message}", args));
        }
        public void Error(string message, params object[] args) {
            UniLog.Error(String.Format($"Discord:Error - {message}", args));
        }
        public LogLevel Level {
            get => LogLevel.Info;
            set {}
        }
    }
}