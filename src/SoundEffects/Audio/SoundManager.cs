using Serilog;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Codecs.FFMpeg;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace SoundEffects.Audio;

public enum SoundPlayOutcome
{
	Started,
	FileNotFound,
	NoAudioDevice,
	UnsupportedFormat,
}

/// <summary>
/// Owns the single audio engine shared by every action. The engine is created lazily on the first
/// play, because the integration is constructed and validated before any session exists, and because
/// a machine without an audio device must not take the plugin down: a play on such a machine fails
/// with <see cref="SoundPlayOutcome.NoAudioDevice"/> rather than throwing.
/// </summary>
public sealed class SoundManager : IDisposable
{
	private sealed class Track(SoundPlayer player, AssetDataProvider provider, FileStream stream, string path)
	{
		public SoundPlayer Player { get; } = player;
		public AssetDataProvider Provider { get; } = provider;
		public FileStream Stream { get; } = stream;
		public string Path { get; } = path;
		public bool Removed { get; set; }
	}

	private readonly ILogger _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly object _tracksLock = new();
	private readonly List<Track> _tracks = [];

	private MiniAudioEngine? _engine;
	private AudioPlaybackDevice? _device;
	private bool _disposed;

	public SoundManager(ILogger logger) => _logger = logger.ForContext<SoundManager>();

	public SoundPlayOutcome StartSound(string? path, double volume, bool overlap, bool loop)
	{
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return SoundPlayOutcome.FileNotFound;
		}

		if (!EnsureInitialized())
		{
			return SoundPlayOutcome.NoAudioDevice;
		}

		try
		{
			// Ask the codec to resample to the device format while decoding. Without a target format a
			// file whose rate differs from the 48 kHz device is left at its own rate and the player's
			// playback-time resampler converts it, which distorts rates far from the device's (a known
			// SoundFlow limitation, e.g. 24 kHz mp3); FFmpeg's resampler is clean. The stream stays open
			// for the whole playback and is disposed with the track.
			var stream = File.OpenRead(path);
			AssetDataProvider? provider = null;
			try
			{
				provider = new AssetDataProvider(_engine!, _device!.Format, stream);
				var player = new SoundPlayer(_engine!, _device!.Format, provider)
				{
					Name = Path.GetFileName(path),
					IsLooping = loop,
					Volume = (float)(Math.Clamp(volume, 0, 100) / 100.0),
				};
				var track = new Track(player, provider, stream, path);
				player.PlaybackEnded += (_, _) => CleanUpTrack(track);

				lock (_tracksLock)
				{
					if (!overlap)
					{
						RemoveAllTracksUnderLock();
					}

					_device!.MasterMixer.AddComponent(player);
					_tracks.Add(track);
				}

				player.Play();
				return SoundPlayOutcome.Started;
			}
			catch
			{
				provider?.Dispose();
				stream.Dispose();
				throw;
			}
		}
		catch (Exception ex)
		{
			_logger.Warning(ex, "Could not start playback of {File}", path);
			return SoundPlayOutcome.UnsupportedFormat;
		}
	}

	public bool IsPlaying(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		lock (_tracksLock)
		{
			return _tracks.Any(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
		}
	}

	public bool IsLooping(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		lock (_tracksLock)
		{
			return _tracks.Any(t =>
				t.Player.IsLooping
				&& string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
		}
	}

	public void StopSound(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		StopAllWhere(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
	}

	public void StopAll()
	{
		StopAllWhere(_ => true);
	}

	public void Dispose()
	{
		lock (_tracksLock)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			foreach (var track in _tracks.ToList())
			{
				CleanUpTrackCore(track);
			}

			_tracks.Clear();
		}

		// Never touch the engine while holding the tracks lock: CleanUpTrackCore may cross the audio
		// thread through the mixer.
		_device?.Dispose();
		_engine?.Dispose();
		_device = null;
		_engine = null;
		_initLock.Dispose();
	}

	private bool EnsureInitialized()
	{
		if (_engine is not null && _device is not null)
		{
			return true;
		}

		_initLock.Wait();
		try
		{
			if (_engine is not null && _device is not null)
			{
				return true;
			}

			if (_disposed)
			{
				return false;
			}

			var engine = new MiniAudioEngine();
			engine.RegisterCodecFactory(new FFmpegCodecFactory());
			var device = engine.InitializePlaybackDevice(null, AudioFormat.Dvd);
			device.Start();
			_engine = engine;
			_device = device;
			_logger.Information("Audio engine initialized on {Device}", device.Info?.Name ?? "system default");
			return true;
		}
		catch (Exception ex)
		{
			_engine?.Dispose();
			_engine = null;
			_device = null;
			_logger.Warning(ex, "Could not initialize the audio output device");
			return false;
		}
		finally
		{
			_initLock.Release();
		}
	}

	private void StopAllWhere(Func<Track, bool> predicate)
	{
		List<Track> matches;
		lock (_tracksLock)
		{
			matches = _tracks.Where(predicate).ToList();
		}

		foreach (var track in matches)
		{
			CleanUpTrack(track);
		}
	}

	private void RemoveAllTracksUnderLock()
	{
		foreach (var track in _tracks.ToList())
		{
			CleanUpTrackCore(track);
		}

		_tracks.Clear();
	}

	/// <summary>
	/// Called from the audio thread when a track reaches its end. The mixer removal is cheap and the
	/// provider is disposed right away; a torn-down player is unreachable, so its own dispose is
	/// skipped to keep the callback short.
	/// </summary>
	private void CleanUpTrack(Track track)
	{
		lock (_tracksLock)
		{
			if (track.Removed)
			{
				return;
			}

			track.Removed = true;
			_tracks.Remove(track);
			CleanUpTrackCore(track);
		}
	}

	private void CleanUpTrackCore(Track track)
	{
		try
		{
			if (_device?.MasterMixer is { } mixer)
			{
				mixer.RemoveComponent(track.Player);
			}
		}
		catch (Exception ex)
		{
			_logger.Warning(ex, "Could not stop a sound cleanly");
		}

		track.Provider.Dispose();
		track.Stream.Dispose();
	}
}