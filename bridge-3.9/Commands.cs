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
    public void Initialize()
    {
        // Nessuna modifica al disegno e nessuna registrazione manuale:
        // AutoCAD registra i CommandMethod dell'assembly nel gruppo dedicato.
    }

    public void Terminate()
    {
    }
}

public static class Commands
{
    public const string GroupName = "ETIT_COMMAND_BRIDGE_39";

    [CommandMethod(GroupName, "ETIT_ARCTEXT_CMD", "ArcTextLocalName", CommandFlags.Modal | CommandFlags.Redraw)]
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
            // Richiamo qualificato: evita qualunque ricorsione con il nome locale ARCTEXT
            // e lascia intatto il comando Autodesk originale nel gruppo Acet:Arctext.
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
        doc?.Editor.WriteMessage("\nExpress Tools Italiano 3.9 - bridge comandi caricato.");
    }
}
