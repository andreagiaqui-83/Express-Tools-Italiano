using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;

namespace Etit.CommandBridge;

internal static class InProcUiLocalizer
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumWindowsProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder b, int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="SendMessageTimeoutW", SetLastError=true)]
    static extern IntPtr SendText(IntPtr h, uint msg, IntPtr wp, string text, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="SendMessageTimeoutW", SetLastError=true)]
    static extern IntPtr ReadText(IntPtr h, uint msg, UIntPtr capacity, StringBuilder text, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder text, int capacity);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr h);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr h, int pos);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetMenuString(IntPtr h, uint item, StringBuilder b, int n, uint f);
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct MenuInfo { public uint cbSize,fMask,fType,fState,wID; public IntPtr hSubMenu,hbmpChecked,hbmpUnchecked,dwItemData; [MarshalAs(UnmanagedType.LPWStr)] public string? dwTypeData; public uint cch; public IntPtr hbmpItem; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="SetMenuItemInfoW")] static extern bool SetMenuItemInfo(IntPtr menu,uint item,bool byPosition,ref MenuInfo info);
    [DllImport("user32.dll")] static extern bool DrawMenuBar(IntPtr h);

    const uint MF_BYPOSITION=0x400, WM_SETTEXT=0x000C, WM_GETTEXT=0x000D, SMTO_ABORTIFHUNG=2, SMTO_BLOCK=1, MIIM_STRING=0x40;
    static Timer? _timer;
    static int _busy;

    static readonly Dictionary<string,string> M = new Dictionary<string,string>(StringComparer.Ordinal) {
        {"ArcAlignedText Workshop - Create","Testo allineato ad arco - Crea"},
        {"ArcAlignedText Workshop - Modify","Testo allineato ad arco - Modifica"},
        {"ArcAlignedText Workshop","Testo allineato ad arco"},
        {"&File","&File"},{"File","File"},{"&Edit","&Modifica"},{"Edit","Modifica"},{"F&ormat","F&ormato"},{"Format","Formato"},{"&Help","&Guida"},{"Help","Guida"},
        {"&Update to AutoCAD","&Aggiorna in AutoCAD"},{"E&xit to AutoCAD","E&sci in AutoCAD"},
        {"&Undo\tCtrl+Z","&Annulla\tCtrl+Z"},{"&Redo\tCtrl+Y","&Ripeti\tCtrl+Y"},{"Cu&t\tCtrl+X","Ta&glia\tCtrl+X"},{"&Copy\tCtrl+C","&Copia\tCtrl+C"},{"&Paste\tCtrl+V","&Incolla\tCtrl+V"},{"Select all\tCtrl+A","Seleziona tutto\tCtrl+A"},{"Clear\tDel","Cancella\tDel"},
        {"Reverse Text","Inverti testo"},{"Alignment","Allineamento"},{"Left","Sinistra"},{"Right","Destra"},{"Center","Centro"},{"Position","Posizione"},{"Convex side","Lato convesso"},{"Concave side","Lato concavo"},{"Direction","Direzione"},{"Outward from the center","Verso l'esterno dal centro"},{"Inward to the center","Verso l'interno al centro"},{"Typeface","Carattere"},{"&Bold","&Grassetto"},{"&Italic","&Corsivo"},{"&Underline","&Sottolineato"},{"ArcAlignedText &Help","&Guida del testo allineato ad arco"},{"ArcAlignedText Help","Guida del testo allineato ad arco"},
        {"Text:","Testo:"},{"Properties:","Proprietà:"},{"Text height:","Altezza:"},{"Text Height:","Altezza:"},{"Width factor:","Larghezza:"},{"Char spacing:","Spaziatura:"},{"Offset from arc:","Dall'arco:"},{"Offset from left:","Da sinistra:"},{"Offset from right:","Da destra:"},{"Cancel","Annulla"},
        {"Dimension Style Export","Esporta stile di quota"},{"Dimension Style Import","Importa stile di quota"},{"Available Dimension Styles","Stili di quota disponibili"},{"Export Filename","Nome file di esportazione"},{"Import Filename","Nome file di importazione"},{"Import Options","Opzioni di importazione"},{"&Browse...","&Sfoglia..."},{"Text Style Options","Opzioni stile di testo"},{"&Text Style Name Only","Solo &nome stile di testo"},{"&Full Text Style Information","Informazioni &complete stile di testo"},{"&Keep Existing Style","&Mantieni stile esistente"},{"&Overwrite Existing Style","&Sovrascrivi stile esistente"},
        {"LayerWalk","Esplora layer"},{"LayerWalk Alert","Avviso Esplora layer"},{"&Filter","&Filtro"},{"&Purge","&Elimina inutilizzati"},{"&Restore on exit","&Ripristina all'uscita"},{"&Close","&Chiudi"},{"Inspect","Ispeziona"},{"Layers in drawing:","Layer nel disegno:"},{"Layers selected:","Layer selezionati:"},{"Entities on selected layers:","Entità sui layer selezionati:"},{"&Hold Selection","&Mantieni selezione"},{"&Release Selection","&Rilascia selezione"},{"Release &All","Rilascia &tutto"},{"&Select All","&Seleziona tutto"},{"&Clear All","&Deseleziona tutto"},{"&Invert Selection","&Inverti selezione"},{"Select &Unreferenced","Seleziona &non referenziati"},{"Sa&ve Layer State...","Sal&va stato layer..."},{"I&nspect...","I&speziona..."},{"C&opy as Filter","C&opia come filtro"},{"Save Current &Filter","Salva &filtro corrente"},{"&Delete Current Filter","&Elimina filtro corrente"},
        {"Text to MText Settings","Impostazioni Testo in TestoM"},{"&Combine into a single mtext object","&Combina in un unico oggetto TestoM"},{"Sort &top-down","Ordina dall'&alto in basso"},{"&Select order of text","&Ordine di selezione del testo"},{"Text ordering","Ordine del testo"},{"&Word-wrap text","&Testo a capo automatico"},{"&Force uniform line spacing","&Forza interlinea uniforme"},
        {"Edit Propulate Template","Modifica modello Propulate"},{"Propulate Alert","Avviso Propulate"},{"Custom properties:","Proprietà personalizzate:"},{"&Block Attributes:","&Attributi blocco:"},{"&Title:","&Titolo:"},{"&Subject:","&Oggetto:"},{"&Author:","&Autore:"},{"&Keywords:","&Parole chiave:"},{"&Comments:","&Commenti:"},{"&Hyperlink base:","&Base collegamento ipertestuale:"},{"&Last saved by:","&Ultimo salvataggio di:"},{"&Revision number:","&Numero revisione:"},{"&New...","&Nuovo..."},{"&Open...","&Apri..."},{"&Save","&Salva"},{"Save &As...","Salva &con nome..."},{"&Template","&Modello"},{"Insert &Xref list","Inserisci elenco &Xrif"},{"Insert &Font list","Inserisci elenco &font"},{"Insert &Image list","Inserisci elenco &immagini"},{"Insert &Attribute","Inserisci &attributo"},{"&Make active template","&Imposta modello attivo"},{"Fill from Current &Drawing","Compila dal &disegno corrente"},
        {"System Variables","Variabili di sistema"},{"Sysvdlg Alert","Avviso variabili di sistema"},{"&New Value:","&Nuovo valore:"},{"Current Value:","Valore corrente:"},{"Initial Value:","Valore iniziale:"},{"Saved In:","Salvato in:"},{"Type:","Tipo:"},{"&Save All...","&Salva tutto..."},{"&Read...","&Leggi..."},{"&Read Only","&Solo lettura"},
        {"Select Email Addresses","Seleziona indirizzi e-mail"},{"&New address:","&Nuovo indirizzo:"},{"&Add","&Aggiungi"},{"&Current list:","Elenco &corrente:"},{"&Delete","&Elimina"},
        {"Choose Directory","Scegli cartella"},{"Select Starting Search Folder","Seleziona cartella iniziale di ricerca"},{"Edit Text","Modifica testo"},{"File &Name:","&Nome file:"},{"&Directories:","&Cartelle:"},{"List Files of &Type:","Elenca file di &tipo:"},{"Dri&ves:","&Unità:"},{"FTP password","Password FTP"},{"Host:","Host:"},{"Login:","Accesso:"},{"&Password:","&Password:"},
        {"&Save Selected...","&Salva selezionate..."},{"&Save Filtered...","&Salva filtrate..."},
        {"File Access Error","Errore di accesso al file"},{"An internal error has occurred.","Si è verificato un errore interno."},{"Out of memory.","Memoria insufficiente."},
        {"The current layer is turned off!","Il layer corrente è disattivato."},{"Invalid layer state name!","Nome dello stato layer non valido."},{"Overwrite existing layer state?","Sovrascrivere lo stato layer esistente?"},
        {"Save Changes?","Salvare le modifiche?"},{"Drawing must be saved first.","Prima è necessario salvare il disegno."},{"Invalid file type.","Tipo di file non valido."},
        {"(Read-only)","(Sola lettura)"},{"New value out of range.","Il nuovo valore è fuori intervallo."},{"Zero value not allowed.","Il valore zero non è ammesso."},
        {"Can't set read-only sysvar.","Impossibile impostare una variabile di sola lettura."},{"Error updating system variable.","Errore durante l'aggiornamento della variabile di sistema."},
        {"Invalid text height !","Altezza del testo non valida."},{"Text height can not be a negative value !","L'altezza del testo non può essere negativa."},{"Invalid text width scale factor !","Fattore di larghezza del testo non valido."},{"Invalid character spacing value !","Spaziatura dei caratteri non valida."},{"Character spacing can not be a negative value!","La spaziatura dei caratteri non può essere negativa."},{"Invalid text offset !","Distanza del testo non valida."},{"Invalid text !","Testo non valido."},{"Need at least 2 characters to fit !","Servono almeno due caratteri per adattare il testo."},
        {"Unable to display dialog box","Impossibile visualizzare la finestra."},{"Unable to load dialog box","Impossibile caricare la finestra."},{"Value must be positive & nonzero.","Il valore deve essere maggiore di zero."},{"Invalid selection.","Selezione non valida."},
        {"Yes","Sì"},{"&Yes","&Sì"},{"&No","&No"},{"&Cancel","&Annulla"},{"Open","Apri"},{"&Open","&Apri"},{"Save","Salva"},
        {"RTEXT: memory allocation error","RTEXT: errore di allocazione della memoria"},{"RTEXT: file read error","RTEXT: errore di lettura del file"},{"RTEXT: file open error","RTEXT: errore di apertura del file"},
        {"SYSVDLG.DAT file not found.","File SYSVDLG.DAT non trovato."},{"Custom sysvar settings read.","Impostazioni personalizzate delle variabili di sistema lette."},
        {"Cannot open output file","Impossibile aprire il file di destinazione"},{"Cannot open input file","Impossibile aprire il file di origine"},{"Not a valid SHX file","Il file SHX non è valido"},{"Could not read shape index","Impossibile leggere l’indice delle forme"},{"Could not load hash table","Impossibile caricare la tabella hash"},{"Invalid hash table entry size","Dimensione della voce della tabella hash non valida"},
        {"TCASE - change text case","TCASE - modifica maiuscole/minuscole"},{"Sentence case.","Iniziale frase"},{"lowercase","minuscolo"},{"UPPERCASE","MAIUSCOLO"},{"Title","Iniziali maiuscole"},{"tOGGLE cASE.","Inverti maiuscole"}
    };

    static readonly string[] TargetExact = {
        "Dimension Style Export","Esporta stile di quota","Dimension Style Import","Importa stile di quota",
        "LayerWalk","Esplora layer","LayerWalk Alert","Avviso Esplora layer",
        "Text to MText Settings","Impostazioni Testo in TestoM","Edit Propulate Template","Modifica modello Propulate","Propulate Alert","Avviso Propulate",
        "System Variables","Variabili di sistema","Sysvdlg Alert","Avviso variabili di sistema","Select Email Addresses","Seleziona indirizzi e-mail",
        "TCASE - change text case","TCASE - modifica maiuscole/minuscole"
    };

    static bool IsTarget(string s) {
        if(s.StartsWith("ArcAlignedText Workshop",StringComparison.Ordinal) || s.StartsWith("Testo allineato ad arco",StringComparison.Ordinal)) return true;
        if(Regex.IsMatch(s,@"\ALayerWalk - Layers: \d+(?: of \d+)?\z") || Regex.IsMatch(s,@"\AEsplora layer - Layer: \d+(?: di \d+)?\z")) return true;
        foreach(string t in TargetExact) if(s==t) return true;
        return false;
    }
    static string ClassName(IntPtr h) { var b=new StringBuilder(128); GetClassName(h,b,b.Capacity); return b.ToString(); }
    static string Txt(IntPtr h) { int n=GetWindowTextLength(h); if(n<=0 || n>4096) return ""; var b=new StringBuilder(n+2); GetWindowText(h,b,b.Capacity); return b.ToString(); }
    static bool LT(IntPtr h,bool top) {
        string s;
        if(top) s=Txt(h);
        else { string cls=ClassName(h); if(cls!="Static" && cls!="Button") return false; var b=new StringBuilder(4096); UIntPtr count; if(ReadText(h,WM_GETTEXT,(UIntPtr)(uint)b.Capacity,b,SMTO_ABORTIFHUNG|SMTO_BLOCK,100,out count)==IntPtr.Zero) return false; s=b.ToString(); }
        string? t; if(s.Length==0) return false;
        if(!M.TryGetValue(s,out t) && !M.TryGetValue(s.Trim(),out t)) {
            if(!top) return false; var match=Regex.Match(s,@"\ALayerWalk - Layers: (\d+)(?: of (\d+))?\z"); if(!match.Success) return false; t="Esplora layer - Layer: "+match.Groups[1].Value; if(match.Groups[2].Success) t+=" di "+match.Groups[2].Value;
        }
        if(t==s || t is null) return false; UIntPtr result; return SendText(h,WM_SETTEXT,IntPtr.Zero,t,SMTO_ABORTIFHUNG|SMTO_BLOCK,100,out result)!=IntPtr.Zero && result!=UIntPtr.Zero;
    }
    static bool IsContextTarget(IntPtr w,string title,int pid) {
        if(IsTarget(title)) return true;
        IntPtr owner=GetWindow(w,4); for(int i=0;i<8 && owner!=IntPtr.Zero;i++,owner=GetWindow(owner,4)) { uint op; GetWindowThreadProcessId(owner,out op); if(op!=(uint)pid) return false; string t=Txt(owner); if(IsTarget(t)) return true; }
        return false;
    }
    static int LM(IntPtr menu) {
        if(menu==IntPtr.Zero) return 0; int changed=0,n=GetMenuItemCount(menu);
        for(int i=0;i<n;i++) { var b=new StringBuilder(512); GetMenuString(menu,(uint)i,b,b.Capacity,MF_BYPOSITION); string s=b.ToString(); string? t=null; IntPtr sub=GetSubMenu(menu,i); bool mapped=s.Length>0 && M.TryGetValue(s,out t);
            if(!mapped && s.Length>0) { string suffix="",core=s; int tab=core.IndexOf('\t'); if(tab>=0){suffix=core.Substring(tab);core=core.Substring(0,tab);} string plain=core.Replace("&","").Trim(); if(plain=="Guida di ArcAlignedText" || plain=="ArcAlignedText Help"){t="Guida del testo allineato ad arco"+suffix;mapped=true;} }
            if(mapped && t!=s && t is not null) { var info=new MenuInfo(); info.cbSize=(uint)Marshal.SizeOf(typeof(MenuInfo)); info.fMask=MIIM_STRING; info.dwTypeData=t; if(SetMenuItemInfo(menu,(uint)i,true,ref info)) changed++; }
            if(sub!=IntPtr.Zero) changed+=LM(sub);
        }
        return changed;
    }
    public static int Pass(int pid) {
        int changed=0;
        EnumWindows(delegate(IntPtr w,IntPtr p){ uint wp; GetWindowThreadProcessId(w,out wp); if(wp!=(uint)pid || !IsWindowVisible(w)) return true; string title=Txt(w); if(!IsContextTarget(w,title,pid)) return true; if(LT(w,true)) changed++; EnumChildWindows(w,delegate(IntPtr c,IntPtr q){if(LT(c,false))changed++;return true;},IntPtr.Zero); IntPtr m=GetMenu(w); if(m!=IntPtr.Zero){int mc=LM(m);changed+=mc;if(mc>0)DrawMenuBar(w);} return true; },IntPtr.Zero);
        return changed;
    }
    public static void Start() {
        if(_timer!=null) return; int pid=Process.GetCurrentProcess().Id;
        _timer=new Timer(_=>{ if(Interlocked.Exchange(ref _busy,1)!=0) return; try { Pass(pid); } catch { } finally { Volatile.Write(ref _busy,0); } },null,0,75);
    }
    public static void Stop() { var t=Interlocked.Exchange(ref _timer,null); if(t!=null) t.Dispose(); }
}
