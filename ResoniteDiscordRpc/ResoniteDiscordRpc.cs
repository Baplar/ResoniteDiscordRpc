using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using Elements.Core;
using FrooxEngine.Interfacing;
using System.Linq;

namespace ResoniteDiscordRpc;

public class ResoniteDiscordRpc : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "ResoniteDiscordRpc";
	public override string Author => "Baplar";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/Baplar/ResoniteDiscordRpc";

	private static readonly AccessTools.FieldRef<PlatformInterface, IPlatformConnector[]> connectorsField = AccessTools.FieldRefAccess<PlatformInterface, IPlatformConnector[]>("connectors");

	public override void OnEngineInit() {
		Engine.Current.OnReady += () => {
			UniLog.Log("Disposing of existing Discord connector");
			PlatformInterface platformInterface = Engine.Current.PlatformInterface;
			platformInterface.GetConnectors<DiscordConnector>().ForEach(connector => connector.Dispose());
			UniLog.Log("Initializing Discord RPC connector");
			IPlatformConnector connector = new DiscordRpcConnector();
			if (connector.Initialize(platformInterface).Result) {
				var existingConnectors = connectorsField.Invoke(platformInterface);
				connectorsField.Invoke(platformInterface) = existingConnectors.AddItem(connector).ToArray();
				UniLog.Log("Discord RPC connector initialized successfully");
			}
		};
	}
}
