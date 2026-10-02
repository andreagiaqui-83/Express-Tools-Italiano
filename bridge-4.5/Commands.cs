using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
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
        doc.SendStringToExecute("ETIT_ARCTEXT_CMD ", true, false, true);
    }
}

public static class Commands
{
    public const string GroupName = "ETIT_COMMAND_BRIDGE_45";

    [CommandMethod(GroupName, "ETIT_ARCTEXT_CMD", CommandFlags.Modal | CommandFlags.Redraw)]
    public static void ArcText() => RunArcText();

    [CommandMethod(GroupName, "ETIT_BRIDGE_ENABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Enable()
    {
        Plugin.EnableRedirect();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.5 - intercettazione ARCTEXT attiva.");
    }

    [CommandMethod(GroupName, "ETIT_BRIDGE_DISABLE", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Disable()
    {
        Plugin.DisableRedirect();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.5 - intercettazione ARCTEXT disattivata.");
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

    [CommandMethod(GroupName, "ETIT_BRIDGE_STATUS", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Status()
    {
        string state = Plugin.RedirectEnabled ? "ATTIVA" : "DISATTIVA";
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nExpress Tools Italiano 4.5 - bridge caricato; intercettazione ARCTEXT " + state + ".");
    }

    private static ResultBuffer Pick(PromptEntityResult res)
    {
        var rb = new ResultBuffer();
        rb.Add(new TypedValue((short)LispDataType.ListBegin));
        rb.Add(new TypedValue((short)LispDataType.ObjectId, res.ObjectId));
        rb.Add(new TypedValue((short)LispDataType.Point3d, res.PickedPoint));
        rb.Add(new TypedValue((short)LispDataType.ListEnd));
        return rb;
    }

    private static void RunArcText()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        InProcUiLocalizer.Start();
        Editor ed = doc.Editor;
        var options = new PromptEntityOptions("\nSelezionare un arco o un testo allineato ad arco: ");
        PromptEntityResult result = ed.GetEntity(options);

        if (result.Status != PromptStatus.OK)
        {
            if (result.Status != PromptStatus.Cancel)
                ed.WriteMessage("\nNessun oggetto selezionato.");
            return;
        }

        string dxfName = string.Empty;
        try
        {
            using Transaction tr = doc.TransactionManager.StartOpenCloseTransaction();
            DBObject obj = tr.GetObject(result.ObjectId, OpenMode.ForRead);
            dxfName = obj.GetRXClass().DxfName ?? string.Empty;
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\n[ETIT] Impossibile leggere l'oggetto selezionato: " + ex.Message);
            return;
        }

        if (!dxfName.Equals("ARC", StringComparison.OrdinalIgnoreCase) &&
            !dxfName.Equals("ARCALIGNEDTEXT", StringComparison.OrdinalIgnoreCase))
        {
            ed.WriteMessage("\nSelezionare esclusivamente un arco o un testo allineato ad arco.");
            return;
        }

        try
        {
            Plugin.BackendCallInProgress = true;
            // ARCTEXT usa una selezione tipo ENSEL: il backend richiede sia l'entità
            // sia il punto usato per selezionarla. Passando la coppia completa, il
            // prompt Autodesk inglese non deve più essere richiesto una seconda volta.
            using ResultBuffer pick = Pick(result);
            ed.Command(".Acet:Arctext.ARCTEXT", pick);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            ed.WriteMessage("\n[ETIT] ARCTEXT originale non disponibile: " + ex.Message);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\n[ETIT] Errore durante ARCTEXT: " + ex.Message);
        }
        finally
        {
            Plugin.BackendCallInProgress = false;
        }
    }
}
