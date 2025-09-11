﻿#if DEBUG
using System.Diagnostics;
#endif

using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using Ryo.Interfaces;
using Ryo.Reloaded.Audio;
using Ryo.Reloaded.Configuration;
using Ryo.Reloaded.CRI.CriAtomEx;
using Ryo.Reloaded.CRI.Mana;
using Ryo.Reloaded.Movies;
using Ryo.Reloaded.Template;
using SharedScans.Interfaces;
using System.Drawing;
using System.Runtime.InteropServices;
using Ryo.Reloaded.CRI;
using Ryo.Reloaded.CRI.CriWare;

namespace Ryo.Reloaded;

public unsafe class Mod : ModBase, IExports
{
    public const string NAME = "Ryo";

    private readonly IModLoader modLoader;
    private readonly ILogger log;
    private readonly IMod owner;

    private Config config;
    private readonly IModConfig modConfig;

    private readonly string game;

    private readonly CriAtomEx criAtomEx;
    private readonly CriAtomRegistry criAtomRegistry;
    private readonly CriMana criMana;
    private readonly CriWareHooks criHooks;

    private readonly AudioRegistry audioRegistry;
    private readonly AudioService audioService;
    private readonly AudioPreprocessor preprocessor = new();

    private readonly MovieRegistry movieRegistry;
    private readonly MovieService movieService;

    private readonly RyoApi ryoApi;

    public Mod(ModContext context)
    {
        this.modLoader = context.ModLoader;
        this.log = context.Logger;
        this.owner = context.Owner;
        this.config = context.Configuration;
        this.modConfig = context.ModConfig;

#if DEBUG
        Debugger.Launch();
#endif

        this.game = Path.GetFileNameWithoutExtension(this.modLoader.GetAppConfig().AppId);

        Project.Initialize(this.modConfig, this.modLoader, this.log, Color.FromArgb(110, 209, 248), true);
        Log.LogLevel = this.config.LogLevel;
        Log.Debug($"Game: {game}");

        this.modLoader.GetController<ISharedScans>().TryGetTarget(out var scans);

        this.criAtomEx = new(this.game, scans!);
        this.modLoader.AddOrReplaceController<ICriAtomEx>(this.owner, this.criAtomEx);

        this.criAtomRegistry = new();
        this.modLoader.AddOrReplaceController<ICriAtomRegistry>(this.owner, this.criAtomRegistry);

        this.criMana = new(scans!, this.game);
        this.criHooks = new(scans!, this.game);

        this.audioRegistry = new(this.game, this.preprocessor);
        this.audioService = new(this.game, scans!, this.criAtomEx, this.criAtomRegistry, this.audioRegistry);

        this.movieRegistry = new();
        this.movieService = new(scans!, this.criMana, this.movieRegistry);

        this.ryoApi = new(this.criAtomRegistry, this.audioRegistry, this.preprocessor, this.movieRegistry);
        this.modLoader.AddOrReplaceController<IRyoApi>(this.owner, this.ryoApi);

        Project.Scans.AddScan("\"CRI AtomEx/PC\" String", "43 52 49 20 41 74 6F 6D 45 78 2F 50 43", result =>
        {
            var criBuild = Marshal.PtrToStringAnsi(result)!.TrimEnd('\n');
            Log.Debug($"Cri AtomEx Build: {criBuild}");

            var startOfs = criBuild.IndexOf("Ver.", StringComparison.Ordinal) + 4;
            var endOfs = criBuild.IndexOf("Build", StringComparison.Ordinal) - 1;
            var atomExVer = criBuild.Substring(startOfs, endOfs - startOfs);
            
            CriWareConfig.SetAtomExVersion(new(atomExVer));
        });
        
        Project.Scans.AddScan("HCADecoder_SetDecryptionTable" ,"48 85 C9 75 ?? 8D 41 ?? C3 48 85 D2 74 1D", result =>
        {
            var tableFieldOfs = (int*)(result + 0x2B + 0x3);
            Log.Debug($"HCA Decryption Table Field Offset: 0x{*tableFieldOfs:X}");
            CriWareConfig.HcaDecodedEncryptKeyOffset = *tableFieldOfs;
        }, () => Log.Information("Failed to find 'HCADecoder_SetDecryptionTable'. Unencrypted HCA in encrypted games is unsupported."));

        this.modLoader.ModLoading += this.OnModLoading;

        this.ApplyConfig();
    }

    private void OnModLoading(IModV1 newMod, IModConfigV1 newModConfig)
    {
        if (!Project.IsModDependent(newModConfig)) return;

        var modDir = this.modLoader.GetDirectoryForModId(newModConfig.ModId);
        var ryoDir = Path.Join(modDir, "ryo", this.game);
        if (Directory.Exists(ryoDir))
        {
            this.audioRegistry.AddAudioFolder(ryoDir);
            this.movieRegistry.AddMoviePath(ryoDir);
        }
    }

    private void ApplyConfig()
    {
        Log.LogLevel = this.config.LogLevel;
        this.criAtomEx.SetDevMode(this.config.DevMode);
        this.audioService.SetDevMode(this.config.DevMode);
        this.movieService.SetDevMode(this.config.DevMode);
        this.criHooks.SetConfig(this.config);
    }

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        this.config = configuration;
        this.log.WriteLine($"[{this.modConfig.ModId}] Config Updated: Applying");
        this.ApplyConfig();
    }

    public Type[] GetTypes() => [typeof(ICriAtomEx), typeof(IRyoApi), typeof(ICriAtomRegistry)];

    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}