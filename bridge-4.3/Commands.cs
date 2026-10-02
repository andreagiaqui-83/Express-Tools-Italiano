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
    private static bool _redirectPending;
    private static Document? _redirectDocument;
    internal static bool BackendCallInProgress;

    public void Initialize()
    {
        InProcUiLocalizer.Start();
        Application.DocumentManager.DocumentLockModeChanged += OnDocumentLockModeChanged;
        Application.DocumentManager.DocumentLockModeChangeVetoed += OnDocumentLockModeChangeVetoed;
    }

    public void Terminate()
    {
        Application.DocumentManager.DocumentLockModeChanged -= OnDocumentLockModeChanged;
        Application.DocumentManager.DocumentLockModeChangeVetoed -= OnDocumentLockModeChangeVetoed;
        InProcUiLocalizer.Stop();
    }

    private static void OnDocumentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
    {
        if (BackendCallInProgress)
            return;
        if (!string.Equals(e.GlobalCommandName, "ARCTEXT", StringComparison.OrdinalIgnoreCase))
            return;

        _redirectPending = true;
        _redirectDocument = e.Document;
        e.Veto();
    }

    private static void OnDocumentLockModeChangeVetoed(object sender, DocumentLockModeChangeVetoedEventArgs e)
    {
        if (!_redirectPending)
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
    public const string GroupName = "ETIT_COMMAND_BRIDGE_43";

    [CommandMethod(GroupName, "ETIT_ARCTEXT_CMD", "ETIT_ARCTEXT_CMD", CommandFlags.Modal | CommandFlags.Redraw)]
    public static void ArcText()
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
            ed.Command(".Acet:Arctext.ARCTEXT", result.ObjectId);
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
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage("\nExpress Tools Italiano 4.3 - intercettazione ARCTEXT e localizzazione UI in-process attive.");
    }
}
