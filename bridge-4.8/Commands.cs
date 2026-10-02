using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(Etit.NativePromptBridge.Commands))]
[assembly: ExtensionApplication(typeof(Etit.NativePromptBridge.Plugin))]

namespace Etit.NativePromptBridge;

public sealed class Plugin : IExtensionApplication
{
    private static bool _enabled;
    private static bool _redirectPending;
    private static Document? _redirectDocument;
    private static string? _redirectCommand;
    internal static bool BackendCallInProgress;

    public void Initialize()
    {
        // Fail-safe: nessuna intercettazione finché il runtime LISP
        // non verifica che i comandi del bridge siano registrati.
    }

    public void Terminate()
    {
        DisableRedirect();
        BackendCallInProgress = false;
    }

    internal static bool Enabled => _enabled;

    internal static void EnableRedirect()
    {
        if (_enabled) return;
        Application.DocumentManager.DocumentLockModeChanged += OnDocumentLockModeChanged;
        Application.DocumentManager.DocumentLockModeChangeVetoed += OnDocumentLockModeChangeVetoed;
        _enabled = true;
    }

    internal static void DisableRedirect()
    {
        if (!_enabled) return;
        Application.DocumentManager.DocumentLockModeChanged -= OnDocumentLockModeChanged;
        Application.DocumentManager.DocumentLockModeChangeVetoed -= OnDocumentLockModeChangeVetoed;
        _redirectPending = false;
        _redirectDocument = null;
        _redirectCommand = null;
        _enabled = false;
    }

    private static bool IsTarget(string? name) =>
        string.Equals(name, "QLATTACH", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "EDITTIME", StringComparison.OrdinalIgnoreCase);

    private static void OnDocumentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
    {
        if (!_enabled || BackendCallInProgress || !IsTarget(e.GlobalCommandName)) return;

        _redirectPending = true;
        _redirectDocument = e.Document;
        _redirectCommand = e.GlobalCommandName.ToUpperInvariant();
        e.Veto();
    }

    private static void OnDocumentLockModeChangeVetoed(object sender, DocumentLockModeChangeVetoedEventArgs e)
    {
        if (!_enabled || !_redirectPending) return;
        if (_redirectDocument is null || !ReferenceEquals(_redirectDocument, e.Document)) return;
        if (_redirectCommand is null ||
            !string.Equals(_redirectCommand, e.GlobalCommandName, StringComparison.OrdinalIgnoreCase)) return;

        Document doc = _redirectDocument;
        string cmd = _redirectCommand;
        _redirectPending = false;
        _redirectDocument = null;
        _redirectCommand = null;

        doc.SendStringToExecute(
            cmd == "EDITTIME" ? "ETIT_EDITTIME " : "ETIT_QLATTACH ",
            true, false, true);
    }
}

public static class Commands
{
    public const string GroupName = "ETIT_NATIVE_PROMPT_BRIDGE_48";

    [CommandMethod(GroupName, "ETIT_NB48_ENABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Enable() => Plugin.EnableRedirect();

    [CommandMethod(GroupName, "ETIT_NB48_DISABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Disable() => Plugin.DisableRedirect();

    [CommandMethod(GroupName, "ETIT_NB48_STATUS", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Status()
    {
        string state = Plugin.Enabled ? "ATTIVO" : "DISATTIVO";
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.8 - bridge prompt nativi " + state + ".");
    }

    [LispFunction("ETIT_NB48_BACKEND_BEGIN")]
    public static void BackendBegin(ResultBuffer? args) => Plugin.BackendCallInProgress = true;

    [LispFunction("ETIT_NB48_BACKEND_END")]
    public static void BackendEnd(ResultBuffer? args) => Plugin.BackendCallInProgress = false;
}
