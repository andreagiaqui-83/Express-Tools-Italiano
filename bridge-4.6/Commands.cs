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

    public void Initialize()
    {
        // La localizzazione delle finestre resta in-process.
        // L'intercettazione ARCTEXT viene abilitata solo dal runtime LISP
        // dopo verifica che i comandi managed siano davvero registrati.
        InProcUiLocalizer.Start();
    }

    public void Terminate()
    {
        DisableRedirect();
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
        if (!_redirectEnabled)
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

        // 4.6: il front-end pubblico è il wrapper AutoLISP ETIT_ARCTEXT.
        // In questo modo la selezione resta il vero risultato di (entsel),
        // senza conversioni .NET/ResultBuffer prima di entrare nel backend.
        doc.SendStringToExecute("ETIT_ARCTEXT ", true, false, true);
    }
}

public static class Commands
{
    public const string GroupName = "ETIT_COMMAND_BRIDGE_46";

    [CommandMethod(GroupName, "ETIT_BRIDGE_ENABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Enable()
    {
        Plugin.EnableRedirect();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.6 - intercettazione ARCTEXT attiva.");
    }

    [CommandMethod(GroupName, "ETIT_BRIDGE_DISABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Disable()
    {
        Plugin.DisableRedirect();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.6 - intercettazione ARCTEXT disattivata.");
    }

    [CommandMethod(GroupName, "ETIT_BRIDGE_STATUS", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Status()
    {
        string state = Plugin.RedirectEnabled ? "ATTIVA" : "DISATTIVA";
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.6 - bridge caricato; intercettazione ARCTEXT " + state + ".");
    }

    [CommandMethod(GroupName, "ETIT_ARCTEXT_ORIGINALE", CommandFlags.Modal | CommandFlags.Redraw)]
    public static void ArcTextOriginal()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;
        doc.Editor.Command(".Acet:Arctext.ARCTEXT");
    }
}
