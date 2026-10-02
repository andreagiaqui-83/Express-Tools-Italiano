using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(Etit.QLAttachBridge.Commands))]
[assembly: ExtensionApplication(typeof(Etit.QLAttachBridge.Plugin))]

namespace Etit.QLAttachBridge;

public sealed class Plugin : IExtensionApplication
{
    private static bool _enabled;
    private static bool _redirectPending;
    private static Document? _redirectDocument;
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
        _enabled = false;
    }

    private static void OnDocumentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
    {
        if (!_enabled || BackendCallInProgress) return;
        if (!string.Equals(e.GlobalCommandName, "QLATTACH", StringComparison.OrdinalIgnoreCase)) return;

        _redirectPending = true;
        _redirectDocument = e.Document;
        e.Veto();
    }

    private static void OnDocumentLockModeChangeVetoed(object sender, DocumentLockModeChangeVetoedEventArgs e)
    {
        if (!_enabled || !_redirectPending) return;
        if (_redirectDocument is null || !ReferenceEquals(_redirectDocument, e.Document)) return;
        if (!string.Equals(e.GlobalCommandName, "QLATTACH", StringComparison.OrdinalIgnoreCase)) return;

        Document doc = _redirectDocument;
        _redirectPending = false;
        _redirectDocument = null;
        doc.SendStringToExecute("ETIT_QLATTACH ", true, false, true);
    }
}

public static class Commands
{
    public const string GroupName = "ETIT_QLATTACH_BRIDGE_48";

    [CommandMethod(GroupName, "ETIT_QB48_ENABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Enable()
    {
        Plugin.EnableRedirect();
    }

    [CommandMethod(GroupName, "ETIT_QB48_DISABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Disable()
    {
        Plugin.DisableRedirect();
    }

    [CommandMethod(GroupName, "ETIT_QB48_STATUS", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Status()
    {
        string state = Plugin.Enabled ? "ATTIVO" : "DISATTIVO";
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.8 - bridge QLATTACH " + state + ".");
    }

    [LispFunction("ETIT_QB48_BACKEND_BEGIN")]
    public static void BackendBegin(ResultBuffer? args)
    {
        Plugin.BackendCallInProgress = true;
    }

    [LispFunction("ETIT_QB48_BACKEND_END")]
    public static void BackendEnd(ResultBuffer? args)
    {
        Plugin.BackendCallInProgress = false;
    }
}
