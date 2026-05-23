using System;
using System.Linq;
using System.Threading;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using ToshiBox.Common;
using ToshiBox.Util;
using static ToshiBox.Util.Native.Keypress;

namespace ToshiBox.Features;

public class AntiAfkKick : IDisposable
{
    private readonly Config _config;
    private AntiAfkConfig AntiAfkCfg => _config.AntiAfkConfig;
    internal volatile bool running = true;

    public AntiAfkKick(Config config)
    {
        Svc.Log.Debug("Starting AntiAfkKick");
        _config = config;
        DoWork();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void IsEnabled()
    {
        if (AntiAfkCfg.Enabled) EnableAntiAfk();
        else DisableAntiAfk();
    }

    private void EnableAntiAfk()
    {
        Svc.Log.Debug("AntiAfk Enabled");
        running = true;
    }

    private void DisableAntiAfk()
    {
        Svc.Log.Debug("AntiAfk Dsiabled");
        running = false;
    }

    unsafe void DoWork()
    {
        float*[] afkPtrs =
        [
            &UIModule.Instance()->GetInputTimerModule()->AfkTimer,
            &UIModule.Instance()->GetInputTimerModule()->ContentInputTimer,
            &UIModule.Instance()->GetInputTimerModule()->InputTimer,
        ];

        float[] GetTimers()
        {
            var timers = new float[afkPtrs.Length];
            for (int i = 0; i < timers.Length; i++)
            {
                timers[i] = *afkPtrs[i];
            }

            return timers;
        }

        new Thread((ThreadStart)delegate
        {
            while (running)
            {
                try
                {
                    Svc.Log.Verbose($"Afk timers: {string.Join(",", GetTimers().Select(x => x.ToString()))}");
                    if (GetTimers().Max() > AntiAfkCfg.MaxIdle)
                    {
                        if (Native.TryFindGameWindow(out var mwh))
                        {
                            Svc.Log.Verbose(
                                $"Afk timer before: {string.Join(",", GetTimers().Select(x => x.ToString()))}");
                            Svc.Log.Verbose($"Sending anti-afk keypress: {mwh:X16}");
                            new TickScheduler(delegate
                            {
                                SendMessage(mwh, WM_KEYDOWN, (IntPtr)LControlKey, (IntPtr)0);
                                new TickScheduler(delegate
                                {
                                    SendMessage(mwh, WM_KEYUP, (IntPtr)LControlKey, (IntPtr)0);
                                    Svc.Log.Verbose(
                                        $"Afk timer after: {string.Join(",", GetTimers().Select(x => x.ToString()))}");
                                }, Svc.Framework, 200);
                            }, Svc.Framework, 0);
                        }
                        else
                        {
                            Svc.Log.Error("Could not find game window");
                        }
                    }

                    Thread.Sleep(AntiAfkCfg.CheckInterval*1_000);
                }
                catch (Exception e)
                {
                    Svc.Log.Error(e.Message + "\n" + e.StackTrace ?? "");
                }
            }

            Svc.Log.Debug("Thread has stopped");
        }).Start();
    }
}

class TickScheduler : IDisposable
{
    long executeAt;
    Action function;
    IFramework framework;

    public TickScheduler(Action function, IFramework framework, long delayMS = 0)
    {
        this.executeAt = Environment.TickCount64 + delayMS;
        this.function = function;
        this.framework = framework;
        framework.Update += Execute;
    }

    public void Dispose()
    {
        framework.Update -= Execute;
    }

    void Execute(object _)
    {
        if (Environment.TickCount64 < executeAt) return;
        try
        {
            function();
        }
        catch (Exception e)
        {
            Svc.Log.Error(e.Message + "\n" + e.StackTrace ?? "");
        }

        this.Dispose();
    }
}