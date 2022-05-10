using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace HaselTweaks;

public abstract class Tweak
{
    protected Plugin Plugin = null!;

    public string InternalName => GetType().Name;
    public abstract string Name { get; }
    public virtual string Description { get; } = string.Empty;

    public virtual bool CanLoad => true;
    public virtual bool Outdated { get; protected set; } = false;
    public virtual bool Ready { get; protected set; }
    public virtual bool Enabled { get; protected set; }

    protected IEnumerable<PropertyInfo> Hooks => this.GetType()
        .GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(prop =>
            prop.PropertyType.IsGenericType &&
            prop.PropertyType.GetGenericTypeDefinition() == typeof(Hook<>) &&
            prop.CustomAttributes.Any(ca => ca.AttributeType == typeof(AutoHookAttribute)) &&
            prop.CustomAttributes.Any(ca => ca.AttributeType == typeof(SignatureAttribute))
        );

    protected void CallHooks(string methodName)
    {
        foreach (var property in Hooks)
        {
            var hook = property.GetValue(this);
            if (hook == null) continue;

            typeof(Hook<>)
                .MakeGenericType(property.PropertyType.GetGenericArguments().First())
                .GetMethod(methodName)?
                .Invoke(hook, null);
        }
    }

    internal virtual void SetupInternal(Plugin plugin)
    {
        Plugin = plugin;
        try
        {
            SignatureHelper.Initialise(this);
        }
        catch (SignatureException ex)
        {
            Error(ex, $"SignatureException, flagging tweak '{InternalName}' as outdated");
            Outdated = true;
            return;
        }
        Ready = true;
        Setup();
    }

    internal virtual void EnableInternal()
    {
        Enabled = true;
        CallHooks("Enable");
        Enable();
    }

    internal virtual void DisableInternal()
    {
        Enabled = false;
        CallHooks("Disable");
        Disable();
    }

    internal virtual void DisposeInternal()
    {
        Ready = false;
        CallHooks("Dispose");
        Dispose();
    }

    public virtual void Setup() { }
    public virtual void Enable() { }
    public virtual void Disable() { }
    public virtual void Dispose() { }
    public virtual void OnFrameworkUpdate(Framework framework) { }

    #region Logging methods

    protected void Log(string messageTemplate, params object[] values)
        => Information(messageTemplate, values);

    protected void Log(Exception exception, string messageTemplate, params object[] values)
        => Information(exception, messageTemplate, values);

    protected void Verbose(string messageTemplate, params object[] values)
        => PluginLog.Verbose($"[{Name}] {messageTemplate}", values);

    protected void Verbose(Exception exception, string messageTemplate, params object[] values)
        => PluginLog.Verbose(exception, $"[{Name}] {messageTemplate}", values);

    protected void Debug(string messageTemplate, params object[] values)
        => PluginLog.Debug($"[{Name}] {messageTemplate}", values);

    protected void Debug(Exception exception, string messageTemplate, params object[] values)
        => PluginLog.Debug(exception, $"[{Name}] {messageTemplate}", values);

    protected void Information(string messageTemplate, params object[] values)
        => PluginLog.Information($"[{Name}] {messageTemplate}", values);

    protected void Information(Exception exception, string messageTemplate, params object[] values)
        => PluginLog.Information(exception, $"[{Name}] {messageTemplate}", values);

    protected void Warning(string messageTemplate, params object[] values)
        => PluginLog.Warning($"[{Name}] {messageTemplate}", values);

    protected void Warning(Exception exception, string messageTemplate, params object[] values)
        => PluginLog.Warning(exception, $"[{Name}] {messageTemplate}", values);

    protected void Error(string messageTemplate, params object[] values)
        => PluginLog.Error($"[{Name}] {messageTemplate}", values);

    protected void Error(Exception exception, string messageTemplate, params object[] values)
        => PluginLog.Error(exception, $"[{Name}] {messageTemplate}", values);

    protected void Fatal(string messageTemplate, params object[] values)
        => PluginLog.Fatal($"[{Name}] {messageTemplate}", values);

    protected void Fatal(Exception exception, string messageTemplate, params object[] values)
        => PluginLog.Fatal(exception, $"[{Name}] {messageTemplate}", values);

    #endregion
}
