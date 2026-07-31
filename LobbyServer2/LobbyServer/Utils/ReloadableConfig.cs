using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using log4net;
using YamlDotNet.Serialization;

namespace CentralServer.LobbyServer.Utils;

public abstract class ReloadableConfig
{
    // Collapse a burst of events (editors fire several per save) into one reload.
    protected static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);
    protected const int ReadRetries = 5;
        
    private static readonly List<ReloadableConfig> All = new();
    private static readonly Lock RegistryLock = new();

    protected static void Register(ReloadableConfig config)
    {
        lock (RegistryLock)
        {
            All.Add(config);
        }
    }

    public static void ShutdownAll()
    {
        lock (RegistryLock)
        {
            foreach (ReloadableConfig config in All)
            {
                config.Shutdown();
            }

            All.Clear();
        }
    }

    protected abstract void Shutdown();
}

// A YAML config that is loaded lazily on first Get() and hot-reloaded in the background via
// a FileSystemWatcher when the file changes. Usage:
//
//     private static readonly ReloadableConfig<MyConfig> Config = new("Config/my.yaml");
//     public static MyConfig Get() => Config.Get();
//
// Fields read live via Get() pick up edits automatically; anything cached elsewhere at
// construction time will still need a restart.
public class ReloadableConfig<T> : ReloadableConfig where T : new()
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ReloadableConfig));

    private readonly string path;
    private readonly Func<string, T> parse;
    private readonly Lock loadLock = new();

    private T instance;
    private FileSystemWatcher watcher;
    private Timer debounceTimer;
    private bool started;

    public ReloadableConfig(string path, Func<string, T> parse = null)
    {
        this.path = Path.Combine("Config", path);
        this.parse = parse ?? DeserializeYaml;
        Register(this);
    }

    private static T DeserializeYaml(string text)
    {
        return new DeserializerBuilder().Build().Deserialize<T>(text);
    }

    public T Get()
    {
        lock (loadLock)
        {
            if (!started)
            {
                Load();
                StartWatching();
                started = true;
            }

            return instance;
        }
    }

    private void Load()
    {
        if (!File.Exists(path))
        {
            if (instance == null)
            {
                log.Info($"{path} not found, using defaults");
                instance = new T();
            }

            // If the file was removed after we loaded it, keep the last known config.
            return;
        }

        // The writer may still hold the file when a change event fires; retry briefly.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                instance = parse(File.ReadAllText(path)) ?? new T();
                return;
            }
            catch (IOException e) when (attempt < ReadRetries)
            {
                log.Warn($"Could not read {path} (attempt {attempt}), retrying: {e.Message}");
                Thread.Sleep(100);
            }
            catch (Exception e)
            {
                log.Error($"Failed to (re)load {path}, keeping previous configuration", e);
                instance ??= new T();
                return;
            }
        }
    }

    private void StartWatching()
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (dir == null || !Directory.Exists(dir))
        {
            log.Warn($"Cannot watch {path} for changes: directory does not exist");
            return;
        }

        // Watch the directory (not the file), and include FileName so that editors which
        // save via a temp file + rename still trigger a reload.
        watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnChanged;
        watcher.Error += OnError;
        watcher.EnableRaisingEvents = true;
        log.Info($"Watching {path} for changes");
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        lock (loadLock)
        {
            debounceTimer ??= new Timer(_ => Reload());
            debounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Buffer overflow etc. - we may have missed events, so resync.
        log.Warn($"FileSystemWatcher error for {path}, reloading to resync", e.GetException());
        Reload();
    }

    private void Reload()
    {
        lock (loadLock)
        {
            log.Info($"Reloading {path}");
            Load();
        }
    }

    protected override void Shutdown()
    {
        lock (loadLock)
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnChanged;
                watcher.Created -= OnChanged;
                watcher.Deleted -= OnChanged;
                watcher.Renamed -= OnChanged;
                watcher.Error -= OnError;
                watcher.Dispose();
                watcher = null;
            }

            debounceTimer?.Dispose();
            debounceTimer = null;
        }
    }
}