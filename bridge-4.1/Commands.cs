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
    public void Initialize() { }
    public void Terminate() { }
}

public static class Commands
{
    public const string GroupName = "ETIT_COMMAND_BRIDGE_41";

    // IMPORTANTE:
    // Il terzo argomento è il NOME LOCALE del comando, non una chiave .resx.
    // In AutoCAD italiano ARCTEXT deve quindi essere registrato letteralmente.
    [CommandMethod(GroupName, "ETIT_ARCTEXT_CMD", "ARCTEXT", CommandFlags.Modal | CommandFlags.Redraw)]
    public static void ArcText()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

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
            // Il comando Autodesk originale rimane intatto e viene richiamato
            // esplicitamente tramite gruppo qualificato, evitando ricorsione.
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
    }

    [CommandMethod(GroupName, "ETIT_BRIDGE_STATUS", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Status()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage("\nExpress Tools Italiano 4.1 - bridge ARCTEXT caricato.");
    }
}
