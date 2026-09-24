using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundEffects.Actions;
using SoundEffects.Audio;

namespace SoundEffects;

/// <summary>
/// The plugin's one integration. It declares the soundboard actions; the shared audio engine is
/// injected as <see cref="SoundManager"/>, which the actions use for every play.
/// </summary>
public sealed class PluginIntegration : IPluginIntegration
{
	private readonly ILogger _logger;

	// Built by DI, so anything the container knows can be taken here: IHttpClientFactory, IOptions<T>,
	// PluginMetadata, IPluginCatalogNotifier.
	public PluginIntegration(ILogger logger, SoundManager sound)
	{
		_logger = logger.ForContext<PluginIntegration>();
		Actions =
		[
			new PlaySoundAction(sound),
			new PlayStopSoundAction(sound),
			new OverlapSoundAction(sound),
			new LoopSoundAction(sound),
			new StopAllSoundAction(sound),
			new RandomSoundAction(sound),
		];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	/// <summary>
	/// Runs once the session is established, and again after a non-resume reconnect or a configuration
	/// change, so it has to be safe to run repeatedly against an already-initialized process. The audio
	/// engine is deliberately not warmed here: the integration can be validated and initialized on a
	/// machine with no audio device, and the first play initializes the engine lazily.
	/// </summary>
	public Task InitializeAsync(IIntegrationContext context)
	{
		_logger.Information("Initialized.");
		return Task.CompletedTask;
	}

	public Task ShutdownAsync() => Task.CompletedTask;
}