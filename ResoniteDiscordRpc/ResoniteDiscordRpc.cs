using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.Interfacing;
using System.Linq;

namespace ResoniteDiscordRpc;

public class Mod : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.2";
	public override string Name => "ResoniteDiscordRpc";
	public override string Author => "Baplar";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/Baplar/ResoniteDiscordRpc";

	private static readonly AccessTools.FieldRef<PlatformInterface, IPlatformConnector[]> connectorsField = AccessTools.FieldRefAccess<PlatformInterface, IPlatformConnector[]>("connectors");

	private static ModConfiguration Config;

	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config.Save(true);

		Engine.Current.OnReady += () => {
			Msg("Disposing of existing Discord connector");
			PlatformInterface platformInterface = Engine.Current.PlatformInterface;
			platformInterface.GetConnectors<DiscordConnector>().ForEach(connector => connector.Dispose());

			Msg("Initializing Discord RPC connector");
			IPlatformConnector connector = new DiscordRpcConnector();
			if (connector.Initialize(platformInterface).Result) {
				var existingConnectors = connectorsField(platformInterface);
				connectorsField(platformInterface) = existingConnectors.AddItem(connector).ToArray();
				Msg("Discord RPC connector initialized successfully");
			}
		};
	}

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> ThumbnailKey = new("thumbnail", "Show world thumbnail in the rich presence preview", () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> SessionInfoKey = new("sessionInfo", "Add link to the session info page on go.resonite.com", () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> JoinKey = new("join", "Add button to join the session", () => true);

	public static bool Thumbnail => Config.GetValue(ThumbnailKey);
	public static bool SessionInfo => Config.GetValue(SessionInfoKey);
	public static bool Join => Config.GetValue(JoinKey);
}
