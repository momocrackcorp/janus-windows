using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Janus")]
[assembly: AssemblyDescription("Migración segura de carpetas conocidas de Windows")]
[assembly: AssemblyCompany("Omar Aguila")]
[assembly: AssemblyProduct("Janus")]
[assembly: AssemblyCopyright("Copyright © Omar Aguila MMXXVI")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace MigradorSeguro {
  static class Program {
    [STAThread] static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new MainForm());
    }
  }

  sealed class FolderItem {
    public string Label, RegistryName, DefaultName, Source, KnownFolderId;
    public long Size; public int Files; public CheckBox Check; public Label SizeLabel;
  }
  sealed class MigrationStats {public int Copied,Identical,Conflicts;public long CopiedBytes;}

  sealed class MainForm : Form {
    const string UserShell = @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";
    const string Shell = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders";
    readonly List<FolderItem> folders = new List<FolderItem>();
    readonly ComboBox drives = new ComboBox(); readonly TextBox destination = new TextBox();
    readonly TextBox preview = new TextBox(); readonly Label capacity = new Label();
    readonly Label required = new Label(); readonly Label status = new Label();
    readonly Button apply = new Button(); readonly DiskPanel disk = new DiskPanel();
    readonly ProgressBar migrationBar = new ProgressBar(); readonly Label progressSummary = new Label();
    readonly FolderPiePanel folderPie = new FolderPiePanel();
    readonly Color[] folderColors={Color.FromArgb(39,125,161),Color.FromArgb(249,199,79),Color.FromArgb(244,162,97),Color.FromArgb(67,170,139),Color.FromArgb(153,102,204),Color.FromArgb(231,111,81)};

    public MainForm() {
      Text = "Janus — Migración segura de carpetas"; Width = 1080; Height = 780;
      MinimumSize = new Size(920, 680); BackColor = Color.FromArgb(244,246,248);
      try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch {}
      Font = new Font("Segoe UI", 9F); BuildFolders(); BuildUi();
      Shown += async (s,e) => await RefreshData();
    }

    void BuildFolders() {
      folders.Add(NewFolder("Escritorio", "Desktop", "Desktop", "B4BFCC3A-DB2C-424C-B029-7FE99A87C641"));
      folders.Add(NewFolder("Documentos", "Personal", "Documents", "FDD39AD0-238F-46AF-ADB4-6C85480369C7"));
      folders.Add(NewFolder("Descargas", "{374DE290-123F-4565-9164-39C4925E467B}", "Downloads", "374DE290-123F-4565-9164-39C4925E467B"));
      folders.Add(NewFolder("Imágenes", "My Pictures", "Pictures", "33E28130-4E1E-4676-835A-98395C3BC3BB"));
      folders.Add(NewFolder("Música", "My Music", "Music", "4BD8D571-6D19-48D3-BE97-422220080E43"));
      folders.Add(NewFolder("Vídeos", "My Video", "Videos", "18989B1D-99B5-455B-841C-AB7C74E4DDFC"));
    }
    FolderItem NewFolder(string label,string reg,string fallback,string knownFolderId) {
      string value = null;
      using (var key=Registry.CurrentUser.OpenSubKey(UserShell)) value=key==null?null:key.GetValue(reg,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
      if (String.IsNullOrWhiteSpace(value)) value=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),fallback);
      return new FolderItem {Label=label,RegistryName=reg,DefaultName=fallback,KnownFolderId=knownFolderId,Source=Environment.ExpandEnvironmentVariables(value)};
    }

    void BuildUi() {
      var title=new Label{Text="Janus",Font=new Font("Segoe UI",19,FontStyle.Bold),AutoSize=true,Location=new Point(22,16)};
      var subtitle=new Label{Text="Copia, verifica y redirige tus carpetas personales sin borrar los originales.",AutoSize=true,Location=new Point(25,54)};
      Controls.Add(title); Controls.Add(subtitle);
      var about=new Button{Text="Acerca de",Size=new Size(100,27),Location=new Point(940,10),Anchor=AnchorStyles.Top|AnchorStyles.Right}; about.Click+=(s,e)=>{using(var d=new AboutForm())d.ShowDialog(this);}; Controls.Add(about);
      var toolsButton=new Button{Text="Herramientas",Size=new Size(100,27),Location=new Point(940,42),Anchor=AnchorStyles.Top|AnchorStyles.Right};toolsButton.Click+=(s,e)=>{using(var d=new WindowsToolsForm())d.ShowDialog(this);};Controls.Add(toolsButton);
      var left=new Panel{BackColor=Color.White,Location=new Point(22,84),Size=new Size(650,585),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};
      var right=new Panel{BackColor=Color.White,Location=new Point(690,84),Size=new Size(350,585),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right};
      Controls.Add(left); Controls.Add(right);
      left.Controls.Add(Header("1. Destino",16,14));
      destination.SetBounds(18,48,372,27); destination.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; destination.TextChanged+=(s,e)=>UpdatePreview(); left.Controls.Add(destination);
      var create=new Button{Text="Crear carpeta…"}; create.SetBounds(398,47,112,29); create.Anchor=AnchorStyles.Top|AnchorStyles.Right; create.Click+=(s,e)=>CreateContainer(); left.Controls.Add(create);
      var browse=new Button{Text="Examinar…"}; browse.SetBounds(518,47,112,29); browse.Anchor=AnchorStyles.Top|AnchorStyles.Right; browse.Click+=(s,e)=>ChooseDestination(); left.Controls.Add(browse);
      drives.SetBounds(18,84,612,28); drives.DropDownStyle=ComboBoxStyle.DropDownList; drives.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; drives.SelectedIndexChanged+=(s,e)=>UpdateDrive(); left.Controls.Add(drives);
      left.Controls.Add(Header("2. Carpetas",16,124));
      int y=158;
      for(int index=0;index<folders.Count;index++) { var f=folders[index];
        var swatch=new Panel{BackColor=folderColors[index],Location=new Point(18,y+5),Size=new Size(11,11)};
        f.Check=new CheckBox{Text=f.Label,Checked=true,Location=new Point(34,y),Width=105}; f.Check.CheckedChanged+=(s,e)=>UpdatePreview();
        f.SizeLabel=new Label{Text="Calculando…",Location=new Point(142,y+2),Width=82,TextAlign=ContentAlignment.MiddleRight};
        var path=new Label{Text=f.Source,ForeColor=Color.DimGray,Location=new Point(236,y+2),Width=85,AutoEllipsis=true};
        left.Controls.Add(swatch);left.Controls.Add(f.Check); left.Controls.Add(f.SizeLabel); left.Controls.Add(path); y+=32;
      }
      folderPie.SetBounds(330,125,300,225);folderPie.Anchor=AnchorStyles.Top|AnchorStyles.Right;left.Controls.Add(folderPie);
      left.Controls.Add(Header("3. Vista previa origen → destino",16,358));
      preview.SetBounds(18,392,612,172); preview.Multiline=true; preview.ReadOnly=true; preview.ScrollBars=ScrollBars.Both; preview.WordWrap=false; preview.BackColor=Color.FromArgb(247,248,250); preview.Font=new Font("Consolas",8.5F); preview.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; left.Controls.Add(preview);
      right.Controls.Add(Header("Capacidad del disco",16,12)); disk.SetBounds(50,37,250,205); right.Controls.Add(disk);
      capacity.SetBounds(20,240,310,45); capacity.TextAlign=ContentAlignment.TopCenter; right.Controls.Add(capacity);
      required.SetBounds(22,290,306,66); required.Font=new Font("Segoe UI",10,FontStyle.Bold); right.Controls.Add(required);
      var protection=new Label{Text="Protecciones activas",Font=new Font("Segoe UI",10,FontStyle.Bold),Location=new Point(22,365),AutoSize=true}; right.Controls.Add(protection);
      var ptext=new Label{Text="• Nunca sobrescribe archivos\n• Nunca borra los originales\n• Verifica antes de cambiar Windows\n• Bloquea rutas críticas y falta de espacio\n• Crea un respaldo restaurable",Location=new Point(22,391),Size=new Size(306,82)}; right.Controls.Add(ptext);
      var actions=new Panel{Dock=DockStyle.Bottom,Height=92,Padding=new Padding(22,8,22,12),BackColor=Color.White};
      apply.Text="Revisar y aplicar migración"; apply.Dock=DockStyle.Top; apply.Height=32; apply.Click+=async(s,e)=>await ApplyMigration();
      var restore=new Button{Text="Restaurar / reparar rutas…",Dock=DockStyle.Bottom,Height=32}; restore.Click+=(s,e)=>RestoreMenu();
      actions.Controls.Add(apply); actions.Controls.Add(restore); right.Controls.Add(actions); actions.BringToFront();
      var progressArea=new Panel{BackColor=Color.FromArgb(244,246,248),Height=66,Dock=DockStyle.Bottom,Padding=new Padding(22,4,22,7)};
      status.Text="Analizando carpetas…";status.Dock=DockStyle.Top;status.Height=19;progressArea.Controls.Add(status);
      progressSummary.Text="Progreso: 0%  •  Transcurrido: 00:00  •  Restante: --:--  •  Faltan: -- archivos";progressSummary.Dock=DockStyle.Top;progressSummary.Height=19;progressSummary.TextAlign=ContentAlignment.MiddleLeft;progressArea.Controls.Add(progressSummary);progressSummary.BringToFront();
      migrationBar.Minimum=0;migrationBar.Maximum=1000;migrationBar.Value=0;migrationBar.Dock=DockStyle.Bottom;migrationBar.Height=18;progressArea.Controls.Add(migrationBar);Controls.Add(progressArea);progressArea.BringToFront();
    }
    Label Header(string text,int x,int y) { return new Label{Text=text,Font=new Font("Segoe UI",11,FontStyle.Bold),Location=new Point(x,y),AutoSize=true}; }

    async Task RefreshData() {
      drives.Items.Clear();
      foreach(var d in DriveInfo.GetDrives().Where(x=>x.IsReady)) drives.Items.Add(d);
      if(drives.Items.Count>0) drives.SelectedIndex=0;
      await Task.Run(()=> { foreach(var f in folders) { long bytes; int count; Stats(f.Source,out bytes,out count); f.Size=bytes; f.Files=count; BeginInvoke((Action)(()=>f.SizeLabel.Text=FormatBytes(f.Size))); } });
      status.Text="Listo. Selecciona una carpeta base segura."; UpdatePreview();
    }
    void UpdateDrive() {
      var d=drives.SelectedItem as DriveInfo; if(d==null)return;
      disk.Total=d.TotalSize; disk.Free=d.AvailableFreeSpace; disk.Root=d.Name; disk.Invalidate();
      capacity.Text=String.Format("Usado: {0} ({1:P1})\nLibre: {2}",FormatBytes(d.TotalSize-d.AvailableFreeSpace),(double)(d.TotalSize-d.AvailableFreeSpace)/d.TotalSize,FormatBytes(d.AvailableFreeSpace)); UpdatePreview();
    }
    void ChooseDestination() { using(var dlg=new FolderBrowserDialog{Description="Selecciona una carpeta base (no la raíz de la unidad)"}) if(dlg.ShowDialog()==DialogResult.OK) destination.Text=dlg.SelectedPath; }
    void CreateContainer(){using(var pick=new FolderBrowserDialog{Description="Selecciona la unidad o carpeta donde crear la carpeta contenedora"}){if(pick.ShowDialog()!=DialogResult.OK)return;using(var prompt=new NamePrompt()){if(prompt.ShowDialog(this)!=DialogResult.OK)return;try{string name=prompt.FolderName.Trim();if(String.IsNullOrWhiteSpace(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name=="."||name=="..")throw new InvalidOperationException("El nombre de la carpeta no es válido.");string path=Path.Combine(pick.SelectedPath,name);ValidateBase(path,folders.Select(f=>f.Source));if(Directory.Exists(path)){if(MessageBox.Show("La carpeta ya existe. ¿Deseas utilizarla como destino?","Carpeta existente",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;}else Directory.CreateDirectory(path);destination.Text=path;status.Text="Carpeta contenedora lista: "+path;}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo crear la carpeta",MessageBoxButtons.OK,MessageBoxIcon.Error);}}}}
    void UpdatePreview() {
      long total=folders.Where(f=>f.Check.Checked).Sum(f=>f.Size); var lines=new List<string>();
      foreach(var f in folders.Where(x=>x.Check.Checked)) lines.Add(f.Source+Environment.NewLine+"  → "+(String.IsNullOrWhiteSpace(destination.Text)?"(elige destino)":Path.Combine(destination.Text,f.DefaultName)));
      preview.Text=String.Join(Environment.NewLine,lines); var selectedDrive=drives.SelectedItem as DriveInfo; long free=selectedDrive==null?0:selectedDrive.AvailableFreeSpace;
      folderPie.Items=folders.Select((f,i)=>new PieItem{Label=f.Label,Size=f.Check.Checked?f.Size:0,Color=folderColors[i]}).Where(x=>x.Size>0).ToList();folderPie.Invalidate();
      bool ok=!String.IsNullOrWhiteSpace(destination.Text)&&folders.Any(f=>f.Check.Checked)&&free>=total+100L*1024*1024;
      required.Text="Datos a copiar: "+FormatBytes(total)+"\nLibre estimado después: "+FormatBytes(Math.Max(0,free-total))+"\n"+(ok?"✓ Espacio suficiente":"⚠ Revisa destino y espacio"); apply.Enabled=ok;
    }

    async Task ApplyMigration() {
      try {
        var selected=folders.Where(f=>f.Check.Checked).ToList(); string root=ValidateBase(destination.Text,selected.Select(f=>f.Source));
        long needed=selected.Sum(f=>f.Size)+100L*1024*1024; var drive=new DriveInfo(Path.GetPathRoot(root)); if(drive.AvailableFreeSpace<needed) throw new InvalidOperationException("Espacio insuficiente. Faltan "+FormatBytes(needed-drive.AvailableFreeSpace)+".");
        string summary=String.Join("\n\n",selected.Select(f=>"• "+f.Label+": "+FormatBytes(f.Size)+"\n  "+f.Source+"\n  → "+Path.Combine(root,f.DefaultName)));
        if(MessageBox.Show("Se copiarán y verificarán estas carpetas:\n\n"+summary+"\n\nSi el destino ya contiene datos, se fusionarán sin sobrescribir: los idénticos se omiten y los conflictos se guardan con otro nombre.\n\nLos originales NO se borrarán. ¿Continuar?","Confirmación final",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        apply.Enabled=false; string backup=BackupRegistry(); status.Text="Respaldo guardado en "+backup;
        long totalBytes=Math.Max(1,selected.Sum(f=>f.Size)),doneBytes=0;int totalFiles=selected.Sum(f=>f.Files),doneFiles=0;var watch=Stopwatch.StartNew();var stats=new MigrationStats();
        UpdateMigrationProgress(0,totalFiles,0,watch.Elapsed,"Preparando copia…");
        await Task.Run(()=> { foreach(var f in selected) CopyVerified(f.Source,Path.Combine(root,f.DefaultName),stats,(m,bytes,files)=>{Interlocked.Add(ref doneBytes,bytes);Interlocked.Add(ref doneFiles,files);long currentBytes=Interlocked.Read(ref doneBytes);int currentFiles=Volatile.Read(ref doneFiles);BeginInvoke((Action)(()=>UpdateMigrationProgress(currentBytes,totalBytes,currentFiles,totalFiles,watch.Elapsed,m)));}); });
        watch.Stop();UpdateMigrationProgress(totalBytes,totalBytes,totalFiles,totalFiles,watch.Elapsed,"Copia y verificación completadas.");
        var changed=new List<FolderItem>();
        try { foreach(var f in selected){SetKnownFolder(f,Path.Combine(root,f.DefaultName));changed.Add(f);} NotifyShell(); }
        catch { RestoreFile(backup,changed.Select(x=>x.Label)); throw; }
        ApplyDestinationDriveIcon(root); status.Text="Migración completada correctamente. Se aplicó el icono celeste a la unidad destino.";
        using(var report=new SummaryForm(BuildSummary(selected,root,backup,stats,watch.Elapsed)))report.ShowDialog(this);
        if(MessageBox.Show("Windows ya apunta al nuevo destino.\n\nLos originales siguen intactos. ¿Reiniciar el Explorador ahora?","Migración completada",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes) RestartExplorer();
      } catch(Exception ex) { MessageBox.Show(ex.Message+"\n\nNo se borraron archivos.","Operación detenida",MessageBoxButtons.OK,MessageBoxIcon.Error); status.Text="La operación fue detenida de forma segura."; }
      finally { UpdatePreview(); }
    }

    static string ValidateBase(string raw,IEnumerable<string> sources) {
      if(String.IsNullOrWhiteSpace(raw))throw new InvalidOperationException("Selecciona una carpeta de destino."); string full=Path.GetFullPath(raw).TrimEnd(Path.DirectorySeparatorChar);
      if(String.Equals(full,Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("No se permite usar la raíz de una unidad.");
      string low=full.ToLowerInvariant(); string[] critical={Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)};
      if(critical.Where(x=>!String.IsNullOrEmpty(x)).Any(x=>IsWithin(full,x)))throw new InvalidOperationException("La ruta está dentro de una carpeta crítica de Windows.");
      if(sources.Any(s=>IsWithin(full,s)||IsWithin(s,full)))throw new InvalidOperationException("Origen y destino no pueden contenerse entre sí."); return full;
    }
    static bool IsWithin(string child,string parent) { child=Path.GetFullPath(child).TrimEnd('\\')+"\\"; parent=Path.GetFullPath(parent).TrimEnd('\\')+"\\"; return child.StartsWith(parent,StringComparison.OrdinalIgnoreCase); }
    static void Stats(string path,out long total,out int count) { total=0;count=0;if(!Directory.Exists(path))return; foreach(var file in SafeFiles(path)){try{var i=new FileInfo(file);if((i.Attributes&FileAttributes.ReparsePoint)==0){total+=i.Length;count++;}}catch{}} }
    static IEnumerable<string> SafeFiles(string root) { var dirs=new Stack<string>();dirs.Push(root);while(dirs.Count>0){var d=dirs.Pop();string[] files=new string[0],subs=new string[0];try{files=Directory.GetFiles(d);subs=Directory.GetDirectories(d);}catch{}foreach(var f in files)yield return f;foreach(var s in subs)try{if((new DirectoryInfo(s).Attributes&FileAttributes.ReparsePoint)==0)dirs.Push(s);}catch{}} }
    void UpdateMigrationProgress(long doneBytes,long totalBytes,int doneFiles,int totalFiles,TimeSpan elapsed,string current){double ratio=totalBytes<=0?0:Math.Min(1.0,(double)doneBytes/totalBytes);int value=(int)Math.Round(ratio*1000);migrationBar.Value=Math.Max(0,Math.Min(1000,value));TimeSpan? remaining=null;if(ratio>.002&&ratio<1)remaining=TimeSpan.FromSeconds(Math.Max(0,elapsed.TotalSeconds*(1-ratio)/ratio));int missing=Math.Max(0,totalFiles-doneFiles);progressSummary.Text=String.Format("Progreso: {0:0.0}%  •  Transcurrido: {1}  •  Restante: {2}  •  Faltan: {3:N0} archivos",ratio*100,FormatTime(elapsed),remaining.HasValue?FormatTime(remaining.Value):"--:--",missing);status.Text=current;}
    void UpdateMigrationProgress(long doneBytes,int totalFiles,int doneFiles,TimeSpan elapsed,string current){UpdateMigrationProgress(doneBytes,Math.Max(1,folders.Where(f=>f.Check.Checked).Sum(f=>f.Size)),doneFiles,totalFiles,elapsed,current);}
    static string FormatTime(TimeSpan value){if(value.TotalHours>=1)return String.Format("{0:00}:{1:00}:{2:00}",(int)value.TotalHours,value.Minutes,value.Seconds);return String.Format("{0:00}:{1:00}",(int)value.TotalMinutes,value.Seconds);}
    static string BuildSummary(List<FolderItem> selected,string root,string backup,MigrationStats stats,TimeSpan elapsed){var b=new System.Text.StringBuilder();b.AppendLine("MIGRADOR SEGURO — RESUMEN DE OPERACIÓN");b.AppendLine(new String('=',44));b.AppendLine("Fecha: "+DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));b.AppendLine("Versión: "+Application.ProductVersion);b.AppendLine("Destino: "+root);b.AppendLine("Tiempo transcurrido: "+FormatTime(elapsed));b.AppendLine();b.AppendLine("ACCIONES REALIZADAS");b.AppendLine("• Carpetas procesadas: "+selected.Count);b.AppendLine("• Archivos copiados: "+stats.Copied);b.AppendLine("• Archivos idénticos verificados y omitidos: "+stats.Identical);b.AppendLine("• Conflictos conservados con…2216 tokens truncated…ult.OK,Location=new Point(226,92),Size=new Size(84,30)};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,Location=new Point(318,92),Size=new Size(84,30)};Controls.Add(label);Controls.Add(name);Controls.Add(ok);Controls.Add(cancel);AcceptButton=ok;CancelButton=cancel;Shown+=(s,e)=>{name.Focus();name.SelectAll();};}}

  sealed class WindowsToolsForm:Form {
    readonly CheckBox neverNotify=new CheckBox();
    public WindowsToolsForm(){Text="Herramientas de Windows";ClientSize=new Size(900,670);MinimumSize=MaximumSize=new Size(916,709);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Color.White;try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
      var title=new Label{Text="Herramientas de Windows",Font=new Font("Segoe UI",18,FontStyle.Bold),Location=new Point(24,17),AutoSize=true};Controls.Add(title);
      var subtitle=new Label{Text="Diagnóstico, administración y software recomendado.",Location=new Point(27,55),AutoSize=true,ForeColor=Color.DimGray};Controls.Add(subtitle);
      var systemGroup=new GroupBox{Text="Sistema y diagnóstico",Location=new Point(24,86),Size=new Size(408,258)};Controls.Add(systemGroup);
      AddToolButton(systemGroup,"Versión de Windows (Winver)",54,27,(s,e)=>Launch("winver.exe"),300);
      AddToolButton(systemGroup,"Información del sistema (MSInfo32)",54,66,(s,e)=>Launch("msinfo32.exe"),300);
      AddToolButton(systemGroup,"Diagnóstico de DirectX (DxDiag)",54,105,(s,e)=>Launch("dxdiag.exe"),300);
      AddToolButton(systemGroup,"Abrir Terminal",54,144,(s,e)=>OpenTerminal(),300);
      AddToolButton(systemGroup,"Ejecutar SystemInfo",54,183,(s,e)=>Launch("cmd.exe","/k title Información del sistema & systeminfo"),300);
      AddToolButton(systemGroup,"Crear / abrir Modo Dios (Windows 10 y 11)",54,222,(s,e)=>CreateGodMode(),300);
      var uacGroup=new GroupBox{Text="Control de cuentas de usuario (UAC)",Location=new Point(24,357),Size=new Size(408,143)};Controls.Add(uacGroup);
      var uacInfo=new Label{Text="Abre el control oficial de Windows para elegir el nivel de notificaciones.",Location=new Point(18,31),Size=new Size(372,38)};uacGroup.Controls.Add(uacInfo);
      var warning=new Label{Text="Recomendado: conserva el nivel predeterminado de Windows.",ForeColor=Color.FromArgb(166,79,0),Location=new Point(18,69),Size=new Size(372,20)};uacGroup.Controls.Add(warning);
      var applyUac=new Button{Text="Abrir configuración oficial de UAC",Location=new Point(54,98),Size=new Size(300,29)};applyUac.Click+=(s,e)=>Launch("UserAccountControlSettings.exe");uacGroup.Controls.Add(applyUac);
      var photo=new PictureBox{Location=new Point(452,86),Size=new Size(424,258),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White};try{using(Stream imageStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MigradorSeguro.ToolsPhoto.png")){if(imageStream!=null)photo.Image=new Bitmap(imageStream);}}catch{}Controls.Add(photo);
      var oneDriveGroup=new GroupBox{Text="Microsoft OneDrive",Location=new Point(24,510),Size=new Size(408,115)};Controls.Add(oneDriveGroup);
      var oneDriveNote=new Label{Text="Cierra OneDrive, retira su inicio automático y permite restaurarlo. No desinstala ni borra archivos.",Location=new Point(18,25),Size=new Size(372,40),ForeColor=Color.DimGray};oneDriveGroup.Controls.Add(oneDriveNote);
      var stopOneDrive=new Button{Text="Cerrar",Location=new Point(18,75),Size=new Size(112,28)};stopOneDrive.Click+=(s,e)=>CloseOneDrive();oneDriveGroup.Controls.Add(stopOneDrive);
      var disableStartup=new Button{Text="Quitar del inicio",Location=new Point(139,75),Size=new Size(122,28)};disableStartup.Click+=(s,e)=>DisableOneDriveStartup();oneDriveGroup.Controls.Add(disableStartup);
      var restoreStartup=new Button{Text="Restaurar inicio",Location=new Point(270,75),Size=new Size(120,28)};restoreStartup.Click+=(s,e)=>RestoreOneDriveStartup();oneDriveGroup.Controls.Add(restoreStartup);
      var links=new GroupBox{Text="Descargas y sitios oficiales",Location=new Point(452,357),Size=new Size(424,150)};Controls.Add(links);
      AddLinkButton(links,"VLC media player",18,27,"https://www.videolan.org/",185);
      AddLinkButton(links,"Codec Guide",217,27,"https://www.codecguide.com/",185);
      AddLinkButton(links,"WinRAR en español",18,64,"https://www.win-rar.com/predownload.html?&L=6",185);
      AddLinkButton(links,"USB Image Tool",217,64,"https://www.osforensics.com/tools/write-usb-images.html",185);
      AddLinkButton(links,"Adobe Acrobat Reader",18,101,"https://get.adobe.com/es/reader/",185);
      AddLinkButton(links,"Microsoft PC Manager",217,101,"https://pcmanager.microsoft.com/en-us",185);
      var browsers=new GroupBox{Text="Navegadores — descargas oficiales",Location=new Point(452,515),Size=new Size(424,110)};Controls.Add(browsers);
      AddLinkButton(browsers,"Google Chrome",18,27,"https://www.google.com/chrome/download-chrome",116);
      AddLinkButton(browsers,"Mozilla Firefox",145,27,"https://www.mozilla.org/firefox/new/",116);
      AddLinkButton(browsers,"Brave",272,27,"https://brave.com/download/",116);
      AddLinkButton(browsers,"Opera",80,67,"https://www.opera.com/download",116);
      AddLinkButton(browsers,"Comet",208,67,"https://www.perplexity.ai/comet",116);
      var close=new Button{Text="Cerrar",Location=new Point(756,633),Size=new Size(120,31),DialogResult=DialogResult.OK};Controls.Add(close);AcceptButton=close;
    }
    static void AddToolButton(Control parent,string text,int x,int y,EventHandler click,int width=158){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,29)};b.Click+=click;parent.Controls.Add(b);}
    static void AddLinkButton(Control parent,string text,int x,int y,string url,int width=388){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,28),Tag=url,TextAlign=ContentAlignment.MiddleCenter};b.Click+=(s,e)=>OpenUrl(Convert.ToString(((Button)s).Tag));parent.Controls.Add(b);}
    static void Launch(string file,string args=null){try{var p=new ProcessStartInfo(file,args??""){UseShellExecute=true};Process.Start(p);}catch(Exception ex){MessageBox.Show("No se pudo abrir la herramienta:\n"+ex.Message,"Herramientas",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void OpenTerminal(){try{Process.Start(new ProcessStartInfo("wt.exe"){UseShellExecute=true});}catch{Launch("powershell.exe");}}
    static void OpenUrl(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){MessageBox.Show("No se pudo abrir el enlace:\n"+ex.Message,"Enlace",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void CreateGodMode(){try{string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);string path=Path.Combine(desktop,"Modo Dios.{ED7BA470-8E54-465E-825C-99712043E01C}");if(!Directory.Exists(path))Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo("explorer.exe","\""+path+"\""){UseShellExecute=true});MessageBox.Show("La carpeta Modo Dios está disponible en el Escritorio. El mismo identificador funciona en Windows 10 y 11.","Modo Dios",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show("No se pudo crear la carpeta:\n"+ex.Message,"Modo Dios",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void CloseOneDrive(){if(MessageBox.Show("OneDrive se cerrará y su icono desaparecerá del área de notificaciones. No se eliminarán archivos. ¿Continuar?","Cerrar OneDrive",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\OneDrive\OneDrive.exe");if(!File.Exists(exe))throw new FileNotFoundException("No se encontró OneDrive para este usuario.");var p=Process.Start(new ProcessStartInfo(exe,"/shutdown"){UseShellExecute=true});MessageBox.Show("Se solicitó a OneDrive que se cierre. Para impedir que vuelva al iniciar Windows, usa el botón 'Abrir aplicaciones de inicio'.","OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show("No se pudo cerrar OneDrive:\n"+ex.Message,"OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void DisableOneDriveStartup(){if(MessageBox.Show("Primero se cerrará OneDrive y luego se quitará su inicio automático para este usuario. La configuración actual se respaldará para poder restaurarla.\n\nNo se desinstalará OneDrive ni se eliminarán archivos. ¿Continuar?","Quitar OneDrive del inicio",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\OneDrive\OneDrive.exe");if(File.Exists(exe))Process.Start(new ProcessStartInfo(exe,"/shutdown"){UseShellExecute=true});using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)){if(run==null)throw new InvalidOperationException("No se pudo abrir la configuración de inicio del usuario.");object value=run.GetValue("OneDrive",null,RegistryValueOptions.DoNotExpandEnvironmentNames);if(value!=null)using(var backup=Registry.CurrentUser.CreateSubKey(@"Software\MigradorSeguro\Backup"))backup.SetValue("OneDriveRun",Convert.ToString(value),RegistryValueKind.String);run.DeleteValue("OneDrive",false);}MessageBox.Show("OneDrive fue cerrado y retirado del inicio automático. Puedes revertirlo con 'Restaurar inicio'.","OneDrive desactivado",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show("No se pudo cambiar el inicio de OneDrive:\n"+ex.Message,"OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void RestoreOneDriveStartup(){if(MessageBox.Show("Se restaurará el inicio automático de OneDrive para este usuario. ¿Continuar?","Restaurar OneDrive",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{string command=null;using(var backup=Registry.CurrentUser.OpenSubKey(@"Software\MigradorSeguro\Backup")){if(backup!=null)command=backup.GetValue("OneDriveRun") as string;}string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\OneDrive\OneDrive.exe");if(String.IsNullOrWhiteSpace(command)&&File.Exists(exe))command="\""+exe+"\" /background";if(String.IsNullOrWhiteSpace(command))throw new FileNotFoundException("No se encontró un respaldo ni la instalación de OneDrive.");using(var run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))run.SetValue("OneDrive",command,RegistryValueKind.String);if(File.Exists(exe))Process.Start(new ProcessStartInfo(exe,"/background"){UseShellExecute=true});MessageBox.Show("El inicio automático de OneDrive fue restaurado.","OneDrive restaurado",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show("No se pudo restaurar OneDrive:\n"+ex.Message,"OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
  }

  sealed class AboutForm:Form {
    int secretClicks;
    public AboutForm(){Text="Acerca de Janus";ClientSize=new Size(620,550);MinimumSize=MaximumSize=new Size(636,589);StartPosition=FormStartPosition.CenterParent;MaximizeBox=false;MinimizeBox=false;BackColor=Color.White;ForeColor=Color.FromArgb(28,38,52);Icon appIcon=null;try{appIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Icon=appIcon;}catch{}
      var picture=new PictureBox{Location=new Point(28,28),Size=new Size(128,128),SizeMode=PictureBoxSizeMode.Zoom,Cursor=Cursors.Hand};try{using(Stream iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MigradorSeguro.AppIcon.png")){if(iconStream!=null)picture.Image=Image.FromStream(iconStream);else if(appIcon!=null)picture.Image=appIcon.ToBitmap();}}catch{if(appIcon!=null)picture.Image=appIcon.ToBitmap();}picture.Click+=(s,e)=>{secretClicks++;if(secretClicks>=5){secretClicks=0;using(var egg=new EasterEggForm())egg.ShowDialog(this);}};Controls.Add(picture);
      var title=new Label{Text="Janus",Font=new Font("Segoe UI",22,FontStyle.Bold),Location=new Point(180,27),AutoSize=true};Controls.Add(title);
      var version=new Label{Text="Versión "+Application.ProductVersion,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(39,125,161),Location=new Point(183,74),AutoSize=true};Controls.Add(version);
      var description=new Label{Text="Herramienta gráfica para migrar y restaurar las carpetas conocidas de Windows de forma segura.",Location=new Point(183,104),Size=new Size(370,55)};Controls.Add(description);
      var line=new Label{BorderStyle=BorderStyle.Fixed3D,Location=new Point(28,178),Size=new Size(564,2)};Controls.Add(line);
      var details=new Label{Text="NOMBRE\nJano (Janus), dios romano de las transiciones, comienzos, finales, puertas y cambios. Sus dos rostros miran al pasado y al futuro: sistema viejo → sistema nuevo.\n\nREALIZACIÓN\n12 de agosto de 2026\n\nCÓMO FUE REALIZADO\nAplicación nativa para Windows, desarrollada en C# con Windows Forms y .NET Framework, API oficiales y verificación SHA-256.",Font=new Font("Segoe UI",9.5F),Location=new Point(30,196),Size=new Size(560,190)};Controls.Add(details);
      var authorshipTitle=new Label{Text="AUTORÍA",Font=new Font("Segoe UI",9.5F),Location=new Point(30,398),AutoSize=true};Controls.Add(authorshipTitle);
      var authorship=new Label{Text="Omar Aguila\nLaboratorios Momocrackcorp\nPueblo Seco, Ñuble, Chile",Font=new Font("Segoe UI",10.5F,FontStyle.Bold),ForeColor=Color.FromArgb(25,92,135),Location=new Point(30,422),Size=new Size(560,72)};Controls.Add(authorship);
      var close=new Button{Text="Cerrar",Location=new Point(472,501),Size=new Size(120,32)};close.Click+=(s,e)=>Close();Controls.Add(close);
    }
  }

  sealed class EasterEggForm:Form {readonly ArcadePanel animation=new ArcadePanel();public EasterEggForm(){Text="MMXXVI";ClientSize=new Size(520,265);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Color.FromArgb(8,17,39);var credit=new Label{Text="Omar Aguila MMXXVI",Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=Color.FromArgb(255,207,46),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=42};Controls.Add(credit);animation.SetBounds(20,48,480,145);Controls.Add(animation);var waka=new Label{Text="wakawakawakawakawaka",Font=new Font("Consolas",13,FontStyle.Bold),ForeColor=Color.FromArgb(255,207,46),TextAlign=ContentAlignment.MiddleCenter,Location=new Point(20,201),Size=new Size(480,35)};Controls.Add(waka);}}

  sealed class SummaryForm:Form {readonly string report;public SummaryForm(string text){report=text;Text="Resumen de la migración";ClientSize=new Size(720,540);MinimumSize=new Size(620,460);StartPosition=FormStartPosition.CenterParent;var title=new Label{Text="Migración completada",Font=new Font("Segoe UI",17,FontStyle.Bold),Dock=DockStyle.Top,Height=50,Padding=new Padding(14,10,0,0)};Controls.Add(title);var box=new TextBox{Text=text,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new Font("Consolas",9F),Dock=DockStyle.Fill,BackColor=Color.White};Controls.Add(box);box.BringToFront();var buttons=new Panel{Dock=DockStyle.Bottom,Height=54,Padding=new Padding(12,10,12,10)};var save=new Button{Text="Guardar resumen…",Dock=DockStyle.Left,Width=145};save.Click+=(s,e)=>Save();var close=new Button{Text="Cerrar",Dock=DockStyle.Right,Width=110,DialogResult=DialogResult.OK};buttons.Controls.Add(save);buttons.Controls.Add(close);Controls.Add(buttons);buttons.BringToFront();AcceptButton=close;}void Save(){using(var d=new SaveFileDialog{Filter="Archivo de texto (*.txt)|*.txt",FileName="Resumen-Janus-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt"})if(d.ShowDialog()==DialogResult.OK){File.WriteAllText(d.FileName,report,System.Text.Encoding.UTF8);MessageBox.Show("Resumen guardado correctamente.","Resumen",MessageBoxButtons.OK,MessageBoxIcon.Information);}}}

  sealed class ArcadePanel:Panel {
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();float x=8;int frame;readonly List<float> dots=new List<float>();
    public ArcadePanel(){DoubleBuffered=true;BackColor=Color.FromArgb(8,17,39);for(int i=0;i<14;i++)dots.Add(42+i*31);timer.Interval=55;timer.Tick+=(s,e)=>{x+=5;frame++;for(int i=0;i<dots.Count;i++)if(dots[i]<x+24)dots[i]+=434;if(x>456)x=8;Invalidate();};timer.Start();}
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;float cy=70;using(var dotBrush=new SolidBrush(Color.FromArgb(255,207,46)))foreach(float dx in dots){float px=((dx-8)%434+434)%434+23;if(Math.Abs(px-(x+20))>20)e.Graphics.FillEllipse(dotBrush,px,cy-4,8,8);}float mouth=(frame%10<5)?34:12;using(var brush=new SolidBrush(Color.FromArgb(255,207,46)))e.Graphics.FillPie(brush,x,cy-24,48,48,mouth,360-mouth*2);using(var eye=new SolidBrush(Color.FromArgb(8,17,39)))e.Graphics.FillEllipse(eye,x+27,cy-16,5,5);using(var pen=new Pen(Color.FromArgb(46,70,108),2))e.Graphics.DrawRectangle(pen,1,1,Width-3,Height-3);}
  }

  sealed class PieItem {public string Label;public long Size;public Color Color;}
  sealed class FolderPiePanel:Panel {
    public List<PieItem> Items=new List<PieItem>();
    public FolderPiePanel(){DoubleBuffered=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;using(var title=new Font("Segoe UI",9.5F,FontStyle.Bold))e.Graphics.DrawString("Distribución seleccionada",title,Brushes.Black,8,2);long total=Items.Sum(x=>x.Size);if(total<=0){using(var empty=new Font("Segoe UI",9F))e.Graphics.DrawString("Selecciona carpetas",empty,Brushes.Gray,38,88);return;}var rect=new Rectangle(10,32,118,118);float start=-90;foreach(var item in Items){float sweep=(float)(360.0*item.Size/total);using(var b=new SolidBrush(item.Color))e.Graphics.FillPie(b,rect,start,Math.Max(.6F,sweep));start+=sweep;}using(var b=new SolidBrush(Color.White))e.Graphics.FillEllipse(b,new Rectangle(44,66,50,50));using(var small=new Font("Segoe UI",8.2F,FontStyle.Bold)){string center=FormatTotal(total);var size=e.Graphics.MeasureString(center,small);e.Graphics.DrawString(center,small,Brushes.Black,69-size.Width/2,84);}int y=32;using(var legend=new Font("Segoe UI",8.4F)){foreach(var item in Items.OrderByDescending(x=>x.Size).Take(6)){using(var b=new SolidBrush(item.Color))e.Graphics.FillRectangle(b,140,y+3,11,11);double pct=100.0*item.Size/total;string text=item.Label+"  "+pct.ToString("0.#")+"%";var area=new RectangleF(157,y,Width-160,21);e.Graphics.DrawString(text,legend,Brushes.Black,area);y+=23;}}}
    static string FormatTotal(long n){string[] u={"B","KB","MB","GB","TB"};double v=n;int i=0;while(v>=1024&&i<u.Length-1){v/=1024;i++;}return (i<2?v.ToString("0"):v.ToString("0.0"))+" "+u[i];}
  }

  sealed class DiskPanel:Panel { public long Total,Free;public string Root="";public DiskPanel(){DoubleBuffered=true;BackColor=Color.White;}protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var r=new Rectangle(25,10,190,190);using(var b=new SolidBrush(Color.FromArgb(220,231,239)))e.Graphics.FillEllipse(b,r);if(Total>0)using(var b=new SolidBrush(Color.FromArgb(39,125,161)))e.Graphics.FillPie(b,r,-90,(float)(360.0*(Total-Free)/Total));using(var b=new SolidBrush(Color.White))e.Graphics.FillEllipse(b,new Rectangle(70,55,100,100));using(var f=new Font("Segoe UI",14,FontStyle.Bold))using(var b=new SolidBrush(Color.Black)){var s=e.Graphics.MeasureString(Root,f);e.Graphics.DrawString(Root,f,b,120-s.Width/2,90);}} }
}
