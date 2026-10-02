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
    public const string GroupName = "ETIT_COMMAND_BRIDGE_42";

    // ARCTEXT è ora il nome GLOBALE reale del comando ETIT.
    // Non dipende da localizedNameId, risorse .resx o alias AutoLISP.
    [CommandMethod(GroupName, "ARCTEXT", CommandFlags.Modal | CommandFlags.Redraw)]
    public static void ArcTextPublic() => RunArcText();

    // Alias stabile per Ribbon, diagnostica e richiami interni.
    [CommandMethod(GroupName, "ETIT_ARCTEXT_CMD", CommandFlags.Modal | CommandFlags.Redraw)]
    public static void ArcTextInternal() => RunArcText();

    private static void RunArcText()
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
            // Richiamo qualificato dell'implementazione Autodesk originale.
            // Il gruppo Acet:Arctext resta caricato e intatto.
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
        doc?.Editor.WriteMessage("\nExpress Tools Italiano 4.2 - bridge ARCTEXT globale caricato.");
    }
}
