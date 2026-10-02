using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(Etit.CommandBridge.Commands))]
[assembly: ExtensionApplication(typeof(Etit.CommandBridge.Plugin))]

namespace Etit.CommandBridge;

public sealed class Plugin : IExtensionApplication
{
    private static bool _redirectEnabled;
    private static bool _redirectPending;
    private static Document? _redirectDocument;
    internal static bool BackendCallInProgress;

    public void Initialize()
    {
        InProcUiLocalizer.Start();
    }

    public void Terminate()
    {
        DisableRedirect();
        BackendCallInProgress = false;
        InProcUiLocalizer.Stop();
    }

    internal static bool RedirectEnabled => _redirectEnabled;

    internal static void EnableRedirect()
    {
        if (_redirectEnabled)
            return;
        Application.DocumentManager.DocumentLockModeChanged += OnDocumentLockModeChanged;
        Application.DocumentManager.DocumentLockModeChangeVetoed += OnDocumentLockModeChangeVetoed;
        _redirectEnabled = true;
    }

    internal static void DisableRedirect()
    {
        if (!_redirectEnabled)
            return;
        Application.DocumentManager.DocumentLockModeChanged -= OnDocumentLockModeChanged;
        Application.DocumentManager.DocumentLockModeChangeVetoed -= OnDocumentLockModeChangeVetoed;
        _redirectPending = false;
        _redirectDocument = null;
        _redirectEnabled = false;
    }

    private static void OnDocumentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
    {
        if (!_redirectEnabled || BackendCallInProgress)
            return;
        if (!string.Equals(e.GlobalCommandName, "ARCTEXT", StringComparison.OrdinalIgnoreCase))
            return;

        _redirectPending = true;
        _redirectDocument = e.Document;
        e.Veto();
    }

    private static void OnDocumentLockModeChangeVetoed(object sender, DocumentLockModeChangeVetoedEventArgs e)
    {
        if (!_redirectEnabled || !_redirectPending)
            return;
        if (_redirectDocument is null || !ReferenceEquals(_redirectDocument, e.Document))
            return;
        if (!string.Equals(e.GlobalCommandName, "ARCTEXT", StringComparison.OrdinalIgnoreCase))
            return;

        Document doc = _redirectDocument;
        _redirectPending = false;
        _redirectDocument = null;
        doc.SendStringToExecute("ETIT_ARCTEXT ", true, false, true);
    }
}

public static class Commands
{
    public const string GroupName = "ETIT_COMMAND_BRIDGE_47";

    // Alias mantenuto per verifica/runtime e compatibilità con le release 4.x.
    [CommandMethod(GroupName, "ETIT_ARCTEXT_CMD", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void ArcTextAlias()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        doc?.SendStringToExecute("ETIT_ARCTEXT ", true, false, true);
    }

    [CommandMethod(GroupName, "ETIT_BRIDGE_ENABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Enable()
    {
        Plugin.EnableRedirect();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.7 - intercettazione ARCTEXT attiva.");
    }

    [CommandMethod(GroupName, "ETIT_BRIDGE_DISABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Disable()
    {
        Plugin.DisableRedirect();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.7 - intercettazione ARCTEXT disattivata.");
    }

    [CommandMethod(GroupName, "ETIT_BRIDGE_STATUS", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Status()
    {
        string state = Plugin.RedirectEnabled ? "ATTIVA" : "DISATTIVA";
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.7 - bridge caricato; intercettazione ARCTEXT " + state + ".");
    }

    [CommandMethod(GroupName, "ETIT_ARCTEXT_ORIGINALE", CommandFlags.Modal | CommandFlags.Redraw)]
    public static void ArcTextOriginal()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;
        try
        {
            Plugin.BackendCallInProgress = true;
            doc.Editor.Command(".Acet:Arctext.ARCTEXT");
        }
        finally
        {
            Plugin.BackendCallInProgress = false;
        }
    }

    [LispFunction("ETIT_BRIDGE_BACKEND_BEGIN")]
    public static void BackendBegin(ResultBuffer? args)
    {
        Plugin.BackendCallInProgress = true;
    }

    [LispFunction("ETIT_BRIDGE_BACKEND_END")]
    public static void BackendEnd(ResultBuffer? args)
    {
        Plugin.BackendCallInProgress = false;
    }
}
