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
[assembly: AssemblyVersion("2.0.9.0")]
[assembly: AssemblyFileVersion("2.0.9.0")]

namespace MigradorSeguro {
  static class Program {
    [STAThread] static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      using (var splash=new SplashForm()) splash.ShowDialog();
      Application.Run(new MainForm());
    }
  }

  sealed class SplashForm:Form {
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    public SplashForm(){
      FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterScreen;ShowInTaskbar=false;TopMost=true;
      ClientSize=new Size(620,620);BackColor=Color.White;
      var picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White};
      try{using(Stream imageStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MigradorSeguro.Splash.png")){if(imageStream!=null)picture.Image=new Bitmap(imageStream);}}catch{}
      Controls.Add(picture);timer.Interval=1900;timer.Tick+=(s,e)=>{timer.Stop();Close();};Shown+=(s,e)=>timer.Start();
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
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
    static string BuildSummary(List<FolderItem> selected,string root,string backup,MigrationStats stats,TimeSpan elapsed){var b=new System.Text.StringBuilder();b.AppendLine("MIGRADOR SEGURO — RESUMEN DE OPERACIÓN");b.AppendLine(new String('=',44));b.AppendLine("Fecha: "+DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));b.AppendLine("Versión: "+Application.ProductVersion);b.AppendLine("Destino: "+root);b.AppendLine("Tiempo transcurrido: "+FormatTime(elapsed));b.AppendLine();b.AppendLine("ACCIONES REALIZADAS");b.AppendLine("• Carpetas procesadas: "+selected.Count);b.AppendLine("• Archivos copiados: "+stats.Copied);b.AppendLine("• Archivos idénticos verificados y omitidos: "+stats.Identical);b.AppendLine("• Conflictos conservados con nombre nuevo: "+stats.Conflicts);b.AppendLine("• Datos copiados: "+FormatBytes(stats.CopiedBytes));b.AppendLine("• Archivos procesados en total: "+(stats.Copied+stats.Identical));b.AppendLine();b.AppendLine("CARPETAS");foreach(var f in selected)b.AppendLine("• "+f.Label+": "+f.Files+" archivos, "+FormatBytes(f.Size)+"\r\n  "+f.Source+" → "+Path.Combine(root,f.DefaultName));b.AppendLine();b.AppendLine("Respaldo de rutas: "+backup);b.AppendLine("Los archivos originales se conservaron.");return b.ToString();}
    static void CopyVerified(string src,string dst,MigrationStats stats,Action<string,long,int> progress) {
      Directory.CreateDirectory(dst);
      foreach(var dir in SafeDirectories(src)) {
        string rel=dir.Substring(src.TrimEnd('\\').Length).TrimStart('\\'); string target=String.IsNullOrEmpty(rel)?dst:Path.Combine(dst,rel);
        Directory.CreateDirectory(target); try { File.SetAttributes(target,File.GetAttributes(dir)); Directory.SetLastWriteTimeUtc(target,Directory.GetLastWriteTimeUtc(dir)); } catch {}
      }
      foreach(var file in SafeFiles(src)){string rel=file.Substring(src.TrimEnd('\\').Length).TrimStart('\\');string target=Path.Combine(dst,rel);Directory.CreateDirectory(Path.GetDirectoryName(target));long fileBytes=0;try{fileBytes=new FileInfo(file).Length;}catch{}if(File.Exists(target)){if(FilesEqual(file,target)){Interlocked.Increment(ref stats.Identical);progress("Ya existe idéntico, verificado: "+rel,fileBytes,1);continue;}target=ConflictName(target);Interlocked.Increment(ref stats.Conflicts);}File.Copy(file,target,false);if(!FilesEqual(file,target))throw new IOException("Falló la verificación de "+Path.GetFileName(file));Interlocked.Increment(ref stats.Copied);Interlocked.Add(ref stats.CopiedBytes,fileBytes);progress("Copiando y verificando: "+rel,fileBytes,1);}
      try { File.SetAttributes(dst,File.GetAttributes(src)|FileAttributes.ReadOnly); } catch {}
    }
    static bool FilesEqual(string a,string b){var fa=new FileInfo(a);var fb=new FileInfo(b);if(fa.Length!=fb.Length)return false;using(var sha=SHA256.Create())using(var sa=File.OpenRead(a))using(var sb=File.OpenRead(b)){return sha.ComputeHash(sa).SequenceEqual(sha.ComputeHash(sb));}}
    static string ConflictName(string path){string dir=Path.GetDirectoryName(path),name=Path.GetFileNameWithoutExtension(path),ext=Path.GetExtension(path),stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");string candidate=Path.Combine(dir,name+" (migrado "+stamp+")"+ext);int n=2;while(File.Exists(candidate)){candidate=Path.Combine(dir,name+" (migrado "+stamp+"-"+n+")"+ext);n++;}return candidate;}
    static IEnumerable<string> SafeDirectories(string root) { yield return root;var dirs=new Stack<string>();dirs.Push(root);while(dirs.Count>0){var d=dirs.Pop();string[] subs=new string[0];try{subs=Directory.GetDirectories(d);}catch{}foreach(var s in subs){bool normal=false;try{normal=(new DirectoryInfo(s).Attributes&FileAttributes.ReparsePoint)==0;}catch{}if(normal){yield return s;dirs.Push(s);}}} }
    string BackupRegistry() { var data=new Dictionary<string,object>();data["version"]=1;data["created"]=DateTime.Now.ToString("s");var values=new Dictionary<string,string>();foreach(var f in folders)values[f.Label]=f.Source;data["folders"]=values;string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Respaldos Migrador Seguro");Directory.CreateDirectory(dir);string path=Path.Combine(dir,"known-folders-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");File.WriteAllText(path,new JavaScriptSerializer().Serialize(data));return path; }
    static void SetRegistry(string name,string value) { using(var k=Registry.CurrentUser.OpenSubKey(UserShell,true))k.SetValue(name,value,RegistryValueKind.ExpandString);using(var k=Registry.CurrentUser.CreateSubKey(Shell))k.SetValue(name,Environment.ExpandEnvironmentVariables(value),RegistryValueKind.String); }
    static void SetKnownFolder(FolderItem folder,string value) { Directory.CreateDirectory(value);var id=new Guid(folder.KnownFolderId);int hr=SHSetKnownFolderPath(ref id,0,IntPtr.Zero,value);if(hr!=0)Marshal.ThrowExceptionForHR(hr);SetRegistry(folder.RegistryName,value);try{File.SetAttributes(value,File.GetAttributes(value)|FileAttributes.ReadOnly);}catch{} }
    void RestoreMenu() { var choice=MessageBox.Show("Sí: reparar ahora las rutas e iconos ya configurados.\n\nNo: restaurar rutas desde un respaldo JSON.\n\nCancelar: no hacer cambios.","Restaurar / reparar",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);if(choice==DialogResult.Yes)RepairCurrent();else if(choice==DialogResult.No)Restore(); }
    void RepairCurrent(){try{foreach(var f in folders){string current=f.Source;using(var k=Registry.CurrentUser.OpenSubKey(UserShell)){var v=k==null?null:k.GetValue(f.RegistryName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;if(!String.IsNullOrWhiteSpace(v))current=Environment.ExpandEnvironmentVariables(v);}if(Directory.Exists(current))SetKnownFolder(f,current);}NotifyShell();MessageBox.Show("Rutas, atributos e iconos registrados nuevamente. Reiniciaremos el Explorador.","Reparación completada",MessageBoxButtons.OK,MessageBoxIcon.Information);RestartExplorer();}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo reparar",MessageBoxButtons.OK,MessageBoxIcon.Error);} }
    void Restore() { using(var d=new OpenFileDialog{Filter="Respaldo JSON (*.json)|*.json",Title="Selecciona el respaldo"})if(d.ShowDialog()==DialogResult.OK&&MessageBox.Show("Se restaurarán las rutas. No se moverán ni borrarán archivos. ¿Continuar?","Restaurar",MessageBoxButtons.YesNo)==DialogResult.Yes){try{RestoreFile(d.FileName,null);NotifyShell();if(MessageBox.Show("Rutas restauradas. ¿Reiniciar el Explorador?","Restaurado",MessageBoxButtons.YesNo)==DialogResult.Yes)RestartExplorer();}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo restaurar",MessageBoxButtons.OK,MessageBoxIcon.Error);}} }
    void RestoreFile(string path,IEnumerable<string> only) { var obj=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(path));var vals=(Dictionary<string,object>)obj["folders"];var set=only==null?null:new HashSet<string>(only);foreach(var f in folders)if((set==null||set.Contains(f.Label))&&vals.ContainsKey(f.Label))SetKnownFolder(f,Convert.ToString(vals[f.Label])); }
    static void NotifyShell(){SHChangeNotify(0x08000000,0x0000,IntPtr.Zero,IntPtr.Zero);}
    static void ApplyDestinationDriveIcon(string destinationPath){try{string drive=Path.GetPathRoot(destinationPath).Substring(0,1).ToUpperInvariant();string appDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MigradorSeguro");Directory.CreateDirectory(appDir);string iconPath=Path.Combine(appDir,"documentos-celeste-transparente-v109.ico");using(Stream input=Assembly.GetExecutingAssembly().GetManifestResourceStream("MigradorSeguro.DocumentosCeleste.ico"))using(var output=File.Create(iconPath)){if(input==null)throw new IOException("No se encontró el icono integrado.");input.CopyTo(output);}using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\Explorer.exe\Drives\"+drive+@"\DefaultIcon"))key.SetValue("",iconPath+",0",RegistryValueKind.String);using(var legacy=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons\"+drive+@"\DefaultIcon"))legacy.SetValue("",iconPath+",0",RegistryValueKind.String);NotifyShell();}catch(Exception ex){MessageBox.Show("La migración terminó, pero Windows no permitió cambiar el icono de la unidad:\n"+ex.Message,"Aviso de icono",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHSetKnownFolderPath(ref Guid rfid,uint flags,IntPtr token,string path);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    static void RestartExplorer() { try{foreach(var p in Process.GetProcessesByName("explorer"))p.Kill();Process.Start("explorer.exe");}catch{} }
    static string FormatBytes(long n) { string[] u={"B","KB","MB","GB","TB"};double v=Math.Max(0,n);int i=0;while(v>=1024&&i<u.Length-1){v/=1024;i++;}return i<2?String.Format("{0:0} {1}",v,u[i]):String.Format("{0:0.0} {1}",v,u[i]); }
  }

  sealed class NamePrompt:Form {readonly TextBox name=new TextBox();public string FolderName{get{return name.Text;}}public NamePrompt(){Text="Crear carpeta contenedora";ClientSize=new Size(420,145);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;var label=new Label{Text="Nombre de la nueva carpeta:",Location=new Point(18,18),AutoSize=true};name.SetBounds(18,45,384,27);name.Text=Environment.UserName;var ok=new Button{Text="Crear",DialogResult=DialogResult.OK,Location=new Point(226,92),Size=new Size(84,30)};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,Location=new Point(318,92),Size=new Size(84,30)};Controls.Add(label);Controls.Add(name);Controls.Add(ok);Controls.Add(cancel);AcceptButton=ok;CancelButton=cancel;Shown+=(s,e)=>{name.Focus();name.SelectAll();};}}

  sealed class WindowsToolsForm:Form {
    readonly CheckBox neverNotify=new CheckBox();
    public WindowsToolsForm(){Text="Herramientas de Windows";ClientSize=new Size(900,695);MinimumSize=MaximumSize=new Size(916,734);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Color.White;try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
      var title=new Label{Text="Herramientas de Windows",Font=new Font("Segoe UI",18,FontStyle.Bold),Location=new Point(24,17),AutoSize=true};Controls.Add(title);
      var subtitle=new Label{Text="Diagnóstico, administración y software recomendado.",Location=new Point(27,55),AutoSize=true,ForeColor=Color.DimGray};Controls.Add(subtitle);
      var systemGroup=new GroupBox{Text="Sistema y diagnóstico",Location=new Point(24,86),Size=new Size(408,234)};Controls.Add(systemGroup);
      AddToolButton(systemGroup,"Versión de Windows (Winver)",54,22,(s,e)=>Launch("winver.exe"),300);
      AddToolButton(systemGroup,"Información del sistema (MSInfo32)",54,57,(s,e)=>Launch("msinfo32.exe"),300);
      AddToolButton(systemGroup,"Diagnóstico de DirectX (DxDiag)",54,92,(s,e)=>Launch("dxdiag.exe"),300);
      AddToolButton(systemGroup,"Abrir Terminal",54,127,(s,e)=>OpenTerminal(),300);
      AddToolButton(systemGroup,"Ejecutar SystemInfo",54,162,(s,e)=>Launch("cmd.exe","/k title Información del sistema & systeminfo"),300);
      AddToolButton(systemGroup,"Crear / abrir Modo Dios (Windows 10 y 11)",54,197,(s,e)=>CreateGodMode(),300);
      var uacGroup=new GroupBox{Text="Control de cuentas de usuario (UAC)",Location=new Point(24,328),Size=new Size(408,112)};Controls.Add(uacGroup);
      var uacInfo=new Label{Text="Abre el control oficial de Windows para elegir el nivel de notificaciones.",Location=new Point(18,24),Size=new Size(372,20)};uacGroup.Controls.Add(uacInfo);
      var warning=new Label{Text="Recomendado: conserva el nivel predeterminado de Windows.",ForeColor=Color.FromArgb(166,79,0),Location=new Point(18,48),Size=new Size(372,20)};uacGroup.Controls.Add(warning);
      var applyUac=new Button{Text="Abrir configuración oficial de UAC",Location=new Point(54,73),Size=new Size(300,27)};applyUac.Click+=(s,e)=>Launch("UserAccountControlSettings.exe");uacGroup.Controls.Add(applyUac);
      var photo=new PictureBox{Location=new Point(452,72),Size=new Size(424,205),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White};try{using(Stream imageStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MigradorSeguro.ToolsPhoto.png")){if(imageStream!=null)photo.Image=new Bitmap(imageStream);}}catch{}Controls.Add(photo);
      var oneDriveGroup=new GroupBox{Text="Microsoft OneDrive",Location=new Point(24,448),Size=new Size(408,98)};Controls.Add(oneDriveGroup);
      var oneDriveNote=new Label{Text="Desactiva la sincronización por directiva, cierra OneDrive y evita su inicio. Requiere administrador.",Location=new Point(18,22),Size=new Size(372,34),ForeColor=Color.DimGray};oneDriveGroup.Controls.Add(oneDriveNote);
      var disableOneDrive=new Button{Text="Desactivar OneDrive",Location=new Point(18,61),Size=new Size(178,27)};disableOneDrive.Click+=(s,e)=>DisableOneDriveStartup();oneDriveGroup.Controls.Add(disableOneDrive);
      var restoreStartup=new Button{Text="Restaurar OneDrive",Location=new Point(212,61),Size=new Size(178,27)};restoreStartup.Click+=(s,e)=>RestoreOneDriveStartup();oneDriveGroup.Controls.Add(restoreStartup);
      var desktopGroup=new GroupBox{Text="Escritorio y menú Inicio",Location=new Point(24,554),Size=new Size(408,130)};Controls.Add(desktopGroup);
      var desktopComputer=new CheckBox{Text="Equipo",Location=new Point(18,22),Size=new Size(165,21)};desktopGroup.Controls.Add(desktopComputer);
      var desktopRecycleBin=new CheckBox{Text="Papelera de reciclaje",Location=new Point(202,22),Size=new Size(185,21)};desktopGroup.Controls.Add(desktopRecycleBin);
      var desktopUserFiles=new CheckBox{Text="Archivos del usuario",Location=new Point(18,45),Size=new Size(165,21)};desktopGroup.Controls.Add(desktopUserFiles);
      var desktopControlPanel=new CheckBox{Text="Panel de control",Location=new Point(202,45),Size=new Size(185,21)};desktopGroup.Controls.Add(desktopControlPanel);
      var desktopNetwork=new CheckBox{Text="Red",Location=new Point(18,68),Size=new Size(90,21)};desktopGroup.Controls.Add(desktopNetwork);
      var startMenuLeft=new CheckBox{Text="Menú Inicio a la izquierda (Windows 11)",Location=new Point(110,68),Size=new Size(277,21)};desktopGroup.Controls.Add(startMenuLeft);
      var applyDesktopIcons=new Button{Text="Aplicar cambios",Location=new Point(202,94),Size=new Size(185,27)};applyDesktopIcons.Click+=(s,e)=>ApplyDesktopSettings(desktopComputer,desktopUserFiles,desktopNetwork,desktopRecycleBin,desktopControlPanel,startMenuLeft);desktopGroup.Controls.Add(applyDesktopIcons);
      LoadDesktopIcons(desktopComputer,desktopUserFiles,desktopNetwork,desktopRecycleBin,desktopControlPanel);
      LoadStartMenuAlignment(startMenuLeft);
      var timeGroup=new GroupBox{Text="Fecha, hora y zona horaria",Location=new Point(452,285),Size=new Size(424,94)};Controls.Add(timeGroup);
      var timeNote=new Label{Text="Activa la detección automática o abre el panel oficial para ajustar el reloj.",Location=new Point(18,23),Size=new Size(388,20),ForeColor=Color.DimGray};timeGroup.Controls.Add(timeNote);
      var automaticZone=new Button{Text="Zona automática",Location=new Point(12,52),Size=new Size(126,28)};automaticZone.Click+=(s,e)=>EnableAutomaticTimeZone();timeGroup.Controls.Add(automaticZone);
      var syncClock=new Button{Text="Sincronizar ahora",Location=new Point(149,52),Size=new Size(126,28)};syncClock.Click+=(s,e)=>SynchronizeClock();timeGroup.Controls.Add(syncClock);
      var adjustClock=new Button{Text="Ajustar reloj",Location=new Point(286,52),Size=new Size(126,28)};adjustClock.Click+=(s,e)=>OpenDateTimeSettings();timeGroup.Controls.Add(adjustClock);
      var links=new GroupBox{Text="Descargas y sitios oficiales",Location=new Point(452,387),Size=new Size(424,150)};Controls.Add(links);
      AddLinkButton(links,"VLC media player",18,27,"https://www.videolan.org/",185);
      AddLinkButton(links,"Codec Guide",217,27,"https://www.codecguide.com/",185);
      AddLinkButton(links,"WinRAR en español",18,64,"https://www.win-rar.com/predownload.html?&L=6",185);
      AddLinkButton(links,"USB Image Tool",217,64,"https://www.osforensics.com/tools/write-usb-images.html",185);
      AddLinkButton(links,"Adobe Acrobat Reader",18,101,"https://get.adobe.com/es/reader/",185);
      AddLinkButton(links,"Microsoft PC Manager",217,101,"https://pcmanager.microsoft.com/en-us",185);
      var browsers=new GroupBox{Text="Navegadores — descargas oficiales",Location=new Point(452,545),Size=new Size(424,110)};Controls.Add(browsers);
      AddLinkButton(browsers,"Google Chrome",18,27,"https://www.google.com/chrome/download-chrome",116);
      AddLinkButton(browsers,"Mozilla Firefox",145,27,"https://www.mozilla.org/firefox/new/",116);
      AddLinkButton(browsers,"Brave",272,27,"https://brave.com/download/",116);
      AddLinkButton(browsers,"Opera",80,67,"https://www.opera.com/download",116);
      AddLinkButton(browsers,"Comet",208,67,"https://www.perplexity.ai/comet",116);
      var close=new Button{Text="Cerrar",Location=new Point(756,660),Size=new Size(120,29),DialogResult=DialogResult.OK};Controls.Add(close);AcceptButton=close;
    }
    static void AddToolButton(Control parent,string text,int x,int y,EventHandler click,int width=158){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,29)};b.Click+=click;parent.Controls.Add(b);}
    static void AddLinkButton(Control parent,string text,int x,int y,string url,int width=388){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,28),Tag=url,TextAlign=ContentAlignment.MiddleCenter};b.Click+=(s,e)=>OpenUrl(Convert.ToString(((Button)s).Tag));parent.Controls.Add(b);}
    static void Launch(string file,string args=null){try{var p=new ProcessStartInfo(file,args??""){UseShellExecute=true};Process.Start(p);}catch(Exception ex){MessageBox.Show("No se pudo abrir la herramienta:\n"+ex.Message,"Herramientas",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void OpenTerminal(){try{Process.Start(new ProcessStartInfo("wt.exe"){UseShellExecute=true});}catch{Launch("powershell.exe");}}
    static void OpenDateTimeSettings(){try{Process.Start(new ProcessStartInfo("ms-settings:dateandtime"){UseShellExecute=true});}catch{Launch("timedate.cpl");}}
    static void SynchronizeClock(){
      if(MessageBox.Show("Windows sincronizará ahora el reloj con su servidor de hora configurado. Se solicitará permiso de administrador. ¿Continuar?","Sincronizar reloj",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      try{
        var command="/c sc start w32time >nul 2>&1 & w32tm /resync";
        var p=Process.Start(new ProcessStartInfo("cmd.exe",command){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
        if(p==null)throw new InvalidOperationException("Windows no pudo iniciar la sincronización.");p.WaitForExit();
        if(p.ExitCode!=0)throw new InvalidOperationException("El servicio de hora de Windows no pudo completar la sincronización.");
        MessageBox.Show("El reloj se sincronizó correctamente con el servidor de hora de Windows.","Hora sincronizada",MessageBoxButtons.OK,MessageBoxIcon.Information);
      }catch(Exception ex){MessageBox.Show("No se pudo sincronizar el reloj:\n"+ex.Message+"\n\nComprueba la conexión a Internet y que no se haya cancelado el permiso de administrador.","Sincronizar reloj",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    static void EnableAutomaticTimeZone(){
      if(MessageBox.Show("Windows activará el servicio de zona horaria automática. Se solicitará permiso de administrador y puede ser necesario habilitar la ubicación del dispositivo. ¿Continuar?","Zona horaria automática",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      try{
        var command="/c reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\tzautoupdate\" /v Start /t REG_DWORD /d 3 /f";
        var p=Process.Start(new ProcessStartInfo("cmd.exe",command){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
        if(p==null)throw new InvalidOperationException("Windows no pudo iniciar la operación administrativa.");p.WaitForExit();
        if(p.ExitCode!=0)throw new InvalidOperationException("Windows no pudo activar el servicio de zona horaria automática.");
        MessageBox.Show("La zona horaria automática quedó activada. Se abrirá el panel de Fecha y hora para que puedas comprobar el interruptor y ajustar o sincronizar el reloj.","Zona horaria automática",MessageBoxButtons.OK,MessageBoxIcon.Information);
        OpenDateTimeSettings();
      }catch(Exception ex){MessageBox.Show("No se pudo activar la zona horaria automática:\n"+ex.Message+"\n\nSi cancelaste el aviso de administrador, no se realizó el cambio.","Fecha y hora",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    static void OpenUrl(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){MessageBox.Show("No se pudo abrir el enlace:\n"+ex.Message,"Enlace",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void CreateGodMode(){try{string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);string path=Path.Combine(desktop,"Modo Dios.{ED7BA470-8E54-465E-825C-99712043E01C}");if(!Directory.Exists(path))Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo("explorer.exe","\""+path+"\""){UseShellExecute=true});MessageBox.Show("La carpeta Modo Dios está disponible en el Escritorio. El mismo identificador funciona en Windows 10 y 11.","Modo Dios",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show("No se pudo crear la carpeta:\n"+ex.Message,"Modo Dios",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    const string DesktopIconsNewStart=@"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
    const string DesktopIconsClassic=@"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu";
    const string DesktopComputer="{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    const string DesktopUserFiles="{59031a47-3f72-44a7-89c5-5595fe6b30ee}";
    const string DesktopNetwork="{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}";
    const string DesktopRecycleBin="{645FF040-5081-101B-9F08-00AA002F954E}";
    const string DesktopControlPanel="{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}";
    static bool DesktopIconVisible(string id,bool defaultVisible){try{using(var key=Registry.CurrentUser.OpenSubKey(DesktopIconsNewStart)){object value=key==null?null:key.GetValue(id);return value==null?defaultVisible:Convert.ToInt32(value)==0;}}catch{return defaultVisible;}}
    static void LoadDesktopIcons(CheckBox computer,CheckBox userFiles,CheckBox network,CheckBox recycleBin,CheckBox controlPanel){computer.Checked=DesktopIconVisible(DesktopComputer,false);userFiles.Checked=DesktopIconVisible(DesktopUserFiles,false);network.Checked=DesktopIconVisible(DesktopNetwork,false);recycleBin.Checked=DesktopIconVisible(DesktopRecycleBin,true);controlPanel.Checked=DesktopIconVisible(DesktopControlPanel,false);}
    static void WriteDesktopIcon(RegistryKey key,string id,bool visible){key.SetValue(id,visible?0:1,RegistryValueKind.DWord);}
    const string ExplorerAdvanced=@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    static bool IsWindows11(){try{using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")){int build;return key!=null&&Int32.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build)&&build>=22000;}}catch{return false;}}
    static void LoadStartMenuAlignment(CheckBox startMenuLeft){bool windows11=IsWindows11();startMenuLeft.Enabled=windows11;if(!windows11){startMenuLeft.Checked=false;startMenuLeft.Text="Menú Inicio a la izquierda (requiere Windows 11)";return;}try{using(var key=Registry.CurrentUser.OpenSubKey(ExplorerAdvanced)){object value=key==null?null:key.GetValue("TaskbarAl");startMenuLeft.Checked=value!=null&&Convert.ToInt32(value)==0;}}catch{startMenuLeft.Checked=false;}}
    static void ApplyDesktopSettings(CheckBox computer,CheckBox userFiles,CheckBox network,CheckBox recycleBin,CheckBox controlPanel,CheckBox startMenuLeft){try{foreach(string path in new[]{DesktopIconsNewStart,DesktopIconsClassic})using(var key=Registry.CurrentUser.CreateSubKey(path)){WriteDesktopIcon(key,DesktopComputer,computer.Checked);WriteDesktopIcon(key,DesktopUserFiles,userFiles.Checked);WriteDesktopIcon(key,DesktopNetwork,network.Checked);WriteDesktopIcon(key,DesktopRecycleBin,recycleBin.Checked);WriteDesktopIcon(key,DesktopControlPanel,controlPanel.Checked);}bool changedStart=false;if(startMenuLeft.Enabled)using(var key=Registry.CurrentUser.CreateSubKey(ExplorerAdvanced)){int desired=startMenuLeft.Checked?0:1;object current=key.GetValue("TaskbarAl");changedStart=current==null||Convert.ToInt32(current)!=desired;key.SetValue("TaskbarAl",desired,RegistryValueKind.DWord);}NotifyDesktop();if(changedStart&&MessageBox.Show("Los iconos fueron actualizados. Para aplicar ahora la posición del menú Inicio es necesario reiniciar el Explorador de Windows.\n\n¿Reiniciarlo ahora?","Escritorio y menú Inicio",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)RestartExplorerForTools();else if(!changedStart)MessageBox.Show("Windows actualizó la configuración seleccionada.","Escritorio y menú Inicio",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show("No se pudo actualizar la configuración:\n"+ex.Message,"Escritorio y menú Inicio",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static void RestartExplorerForTools(){try{foreach(var process in Process.GetProcessesByName("explorer"))process.Kill();Process.Start("explorer.exe");}catch(Exception ex){MessageBox.Show("La configuración fue guardada, pero no se pudo reiniciar el Explorador:\n"+ex.Message,"Menú Inicio",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    static void NotifyDesktop(){SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);}
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    static void CloseOneDrive(){if(MessageBox.Show("OneDrive se cerrará y su icono desaparecerá del área de notificaciones. No se eliminarán archivos. ¿Continuar?","Cerrar OneDrive",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\OneDrive\OneDrive.exe");if(!File.Exists(exe))throw new FileNotFoundException("No se encontró OneDrive para este usuario.");var p=Process.Start(new ProcessStartInfo(exe,"/shutdown"){UseShellExecute=true});MessageBox.Show("Se solicitó a OneDrive que se cierre. Para impedir que vuelva al iniciar Windows, usa el botón 'Abrir aplicaciones de inicio'.","OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show("No se pudo cerrar OneDrive:\n"+ex.Message,"OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    static int RunElevatedRegistry(string arguments){var p=Process.Start(new ProcessStartInfo("reg.exe",arguments){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});if(p==null)throw new InvalidOperationException("Windows no pudo iniciar la operación administrativa.");p.WaitForExit();return p.ExitCode;}
    static string OneDriveExe(){string[] paths={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\OneDrive\OneDrive.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Microsoft OneDrive\OneDrive.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Microsoft OneDrive\OneDrive.exe")};return paths.FirstOrDefault(File.Exists);}
    static void DisableOneDriveStartup(){
      if(MessageBox.Show("Se aplicará la directiva de Windows que DESACTIVA la sincronización de OneDrive en este equipo. También se cerrará el cliente y se quitará de Inicio.\n\nWindows solicitará permiso de administrador. No se desinstalará OneDrive ni se eliminarán archivos. ¿Continuar?","Desactivar OneDrive",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
      try{
        if(RunElevatedRegistry("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\OneDrive\" /v DisableFileSyncNGSC /t REG_DWORD /d 1 /f")!=0)throw new InvalidOperationException("Windows no pudo aplicar la directiva de OneDrive.");
        using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)){if(run==null)throw new InvalidOperationException("No se pudo abrir la configuración de inicio del usuario.");object value=run.GetValue("OneDrive",null,RegistryValueOptions.DoNotExpandEnvironmentNames);if(value!=null)using(var backup=Registry.CurrentUser.CreateSubKey(@"Software\Janus\Backup"))backup.SetValue("OneDriveRun",Convert.ToString(value),RegistryValueKind.String);run.DeleteValue("OneDrive",false);}
        string exe=OneDriveExe();if(!String.IsNullOrWhiteSpace(exe))Process.Start(new ProcessStartInfo(exe,"/shutdown"){UseShellExecute=true});
        MessageBox.Show("OneDrive quedó desactivado por directiva de Windows, cerrado y retirado del inicio automático. Puede ser necesario reiniciar Windows para que todas las aplicaciones reconozcan el cambio.","OneDrive desactivado",MessageBoxButtons.OK,MessageBoxIcon.Information);
      }catch(Exception ex){MessageBox.Show("No se pudo desactivar OneDrive:\n"+ex.Message+"\n\nSi cancelaste el aviso de administrador, no se realizó el cambio.","OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    static void RestoreOneDriveStartup(){
      if(MessageBox.Show("Se retirará la directiva que desactiva OneDrive y se restaurará su inicio automático. Windows solicitará permiso de administrador. ¿Continuar?","Restaurar OneDrive",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      try{
        RunElevatedRegistry("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\OneDrive\" /v DisableFileSyncNGSC /f");
        string command=null;using(var backup=Registry.CurrentUser.OpenSubKey(@"Software\Janus\Backup")){if(backup!=null)command=backup.GetValue("OneDriveRun") as string;}if(String.IsNullOrWhiteSpace(command))using(var oldBackup=Registry.CurrentUser.OpenSubKey(@"Software\MigradorSeguro\Backup")){if(oldBackup!=null)command=oldBackup.GetValue("OneDriveRun") as string;}
        string exe=OneDriveExe();if(String.IsNullOrWhiteSpace(command)&&!String.IsNullOrWhiteSpace(exe))command="\""+exe+"\" /background";if(!String.IsNullOrWhiteSpace(command))using(var run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))run.SetValue("OneDrive",command,RegistryValueKind.String);if(!String.IsNullOrWhiteSpace(exe))Process.Start(new ProcessStartInfo(exe,"/background"){UseShellExecute=true});
        MessageBox.Show("La directiva fue retirada y OneDrive quedó restaurado.","OneDrive restaurado",MessageBoxButtons.OK,MessageBoxIcon.Information);
      }catch(Exception ex){MessageBox.Show("No se pudo restaurar OneDrive:\n"+ex.Message,"OneDrive",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
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
