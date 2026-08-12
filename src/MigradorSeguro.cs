using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Migrador Seguro")]
[assembly: AssemblyDescription("Migración segura de carpetas conocidas de Windows")]
[assembly: AssemblyCompany("Omar Aguila")]
[assembly: AssemblyProduct("Migrador Seguro")]
[assembly: AssemblyCopyright("Copyright © Omar Aguila MMXXVI")]
[assembly: AssemblyVersion("1.0.9.0")]
[assembly: AssemblyFileVersion("1.0.9.0")]

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

  sealed class MainForm : Form {
    const string UserShell = @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";
    const string Shell = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders";
    readonly List<FolderItem> folders = new List<FolderItem>();
    readonly ComboBox drives = new ComboBox(); readonly TextBox destination = new TextBox();
    readonly TextBox preview = new TextBox(); readonly Label capacity = new Label();
    readonly Label required = new Label(); readonly Label status = new Label();
    readonly Button apply = new Button(); readonly DiskPanel disk = new DiskPanel();
    readonly FolderPiePanel folderPie = new FolderPiePanel();
    readonly Color[] folderColors={Color.FromArgb(39,125,161),Color.FromArgb(249,199,79),Color.FromArgb(244,162,97),Color.FromArgb(67,170,139),Color.FromArgb(153,102,204),Color.FromArgb(231,111,81)};

    public MainForm() {
      Text = "Migrador seguro de carpetas de Windows"; Width = 1080; Height = 780;
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
      var title=new Label{Text="Migrador seguro de carpetas",Font=new Font("Segoe UI",19,FontStyle.Bold),AutoSize=true,Location=new Point(22,16)};
      var subtitle=new Label{Text="Copia, verifica y redirige tus carpetas personales sin borrar los originales.",AutoSize=true,Location=new Point(25,54)};
      Controls.Add(title); Controls.Add(subtitle);
      var about=new Button{Text="Acerca de",Size=new Size(100,30),Location=new Point(940,22),Anchor=AnchorStyles.Top|AnchorStyles.Right}; about.Click+=(s,e)=>{using(var d=new AboutForm())d.ShowDialog(this);}; Controls.Add(about);
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
        var path=new Label{Text=f.Source,ForeColor=Color.DimGray,Location=new Point(236,y+2),Width=145,AutoEllipsis=true};
        left.Controls.Add(swatch);left.Controls.Add(f.Check); left.Controls.Add(f.SizeLabel); left.Controls.Add(path); y+=32;
      }
      folderPie.SetBounds(390,125,240,225);folderPie.Anchor=AnchorStyles.Top|AnchorStyles.Right;left.Controls.Add(folderPie);
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
      status.Text="Analizando carpetas…"; status.SetBounds(22,682,1018,25); status.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; Controls.Add(status);
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
        await Task.Run(()=> { foreach(var f in selected) CopyVerified(f.Source,Path.Combine(root,f.DefaultName),m=>BeginInvoke((Action)(()=>status.Text=m))); });
        var changed=new List<FolderItem>();
        try { foreach(var f in selected){SetKnownFolder(f,Path.Combine(root,f.DefaultName));changed.Add(f);} NotifyShell(); }
        catch { RestoreFile(backup,changed.Select(x=>x.Label)); throw; }
        ApplyDestinationDriveIcon(root); status.Text="Migración completada correctamente. Se aplicó el icono celeste a la unidad destino.";
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
    static void CopyVerified(string src,string dst,Action<string> progress) {
      Directory.CreateDirectory(dst);
      foreach(var dir in SafeDirectories(src)) {
        string rel=dir.Substring(src.TrimEnd('\\').Length).TrimStart('\\'); string target=String.IsNullOrEmpty(rel)?dst:Path.Combine(dst,rel);
        Directory.CreateDirectory(target); try { File.SetAttributes(target,File.GetAttributes(dir)); Directory.SetLastWriteTimeUtc(target,Directory.GetLastWriteTimeUtc(dir)); } catch {}
      }
      foreach(var file in SafeFiles(src)){string rel=file.Substring(src.TrimEnd('\\').Length).TrimStart('\\');string target=Path.Combine(dst,rel);Directory.CreateDirectory(Path.GetDirectoryName(target));if(File.Exists(target)){if(FilesEqual(file,target)){progress("Ya existe idéntico, omitido: "+rel);continue;}target=ConflictName(target);progress("Conflicto conservado con otro nombre: "+Path.GetFileName(target));}File.Copy(file,target,false);if(!FilesEqual(file,target))throw new IOException("Falló la verificación de "+Path.GetFileName(file));progress("Copiando "+Path.GetFileName(src)+": "+rel);}
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

  sealed class AboutForm:Form {
    readonly ArcadePanel animation=new ArcadePanel();
    public AboutForm(){Text="Acerca de Migrador Seguro";ClientSize=new Size(520,330);MinimumSize=MaximumSize=new Size(536,369);StartPosition=FormStartPosition.CenterParent;MaximizeBox=false;MinimizeBox=false;BackColor=Color.FromArgb(8,17,39);ForeColor=Color.White;try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
      var title=new Label{Text="Migrador Seguro",Font=new Font("Segoe UI",18,FontStyle.Bold),ForeColor=Color.White,TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=52};Controls.Add(title);
      var credit=new Label{Text="Omar Aguila MMXXVI",Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=Color.FromArgb(255,207,46),TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Top,Height=35};Controls.Add(credit);
      animation.SetBounds(20,92,480,145);animation.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(animation);
      var waka=new Label{Text="wakawakawakawakawaka",Font=new Font("Consolas",13,FontStyle.Bold),ForeColor=Color.FromArgb(255,207,46),TextAlign=ContentAlignment.MiddleCenter,Location=new Point(20,241),Size=new Size(480,35)};Controls.Add(waka);
      var close=new Button{Text="Cerrar",Location=new Point(200,285),Size=new Size(120,30)};close.Click+=(s,e)=>Close();Controls.Add(close);
    }
  }

  sealed class ArcadePanel:Panel {
    readonly Timer timer=new Timer();float x=8;int frame;readonly List<float> dots=new List<float>();
    public ArcadePanel(){DoubleBuffered=true;BackColor=Color.FromArgb(8,17,39);for(int i=0;i<14;i++)dots.Add(42+i*31);timer.Interval=55;timer.Tick+=(s,e)=>{x+=5;frame++;for(int i=0;i<dots.Count;i++)if(dots[i]<x+24)dots[i]+=434;if(x>456)x=8;Invalidate();};timer.Start();}
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;float cy=70;using(var dotBrush=new SolidBrush(Color.FromArgb(255,207,46)))foreach(float dx in dots){float px=((dx-8)%434+434)%434+23;if(Math.Abs(px-(x+20))>20)e.Graphics.FillEllipse(dotBrush,px,cy-4,8,8);}float mouth=(frame%10<5)?34:12;using(var brush=new SolidBrush(Color.FromArgb(255,207,46)))e.Graphics.FillPie(brush,x,cy-24,48,48,mouth,360-mouth*2);using(var eye=new SolidBrush(Color.FromArgb(8,17,39)))e.Graphics.FillEllipse(eye,x+27,cy-16,5,5);using(var pen=new Pen(Color.FromArgb(46,70,108),2))e.Graphics.DrawRectangle(pen,1,1,Width-3,Height-3);}
  }

  sealed class PieItem {public string Label;public long Size;public Color Color;}
  sealed class FolderPiePanel:Panel {
    public List<PieItem> Items=new List<PieItem>();
    public FolderPiePanel(){DoubleBuffered=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var title=new Font("Segoe UI",9,FontStyle.Bold))e.Graphics.DrawString("Distribución seleccionada",title,Brushes.Black,8,2);long total=Items.Sum(x=>x.Size);if(total<=0){e.Graphics.DrawString("Selecciona carpetas",Font,Brushes.Gray,50,90);return;}var rect=new Rectangle(12,28,128,128);float start=-90;foreach(var item in Items){float sweep=(float)(360.0*item.Size/total);using(var b=new SolidBrush(item.Color))e.Graphics.FillPie(b,rect,start,Math.Max(.6F,sweep));start+=sweep;}using(var b=new SolidBrush(Color.White))e.Graphics.FillEllipse(b,new Rectangle(48,64,56,56));using(var small=new Font("Segoe UI",7.5F,FontStyle.Bold)){string center=FormatTotal(total);var size=e.Graphics.MeasureString(center,small);e.Graphics.DrawString(center,small,Brushes.Black,76-size.Width/2,86);}int y=31;using(var legend=new Font("Segoe UI",7.2F)){foreach(var item in Items.OrderByDescending(x=>x.Size).Take(6)){using(var b=new SolidBrush(item.Color))e.Graphics.FillRectangle(b,151,y+3,9,9);double pct=100.0*item.Size/total;string text=item.Label+"  "+pct.ToString("0.#")+"%";e.Graphics.DrawString(text,legend,Brushes.Black,164,y);y+=22;}}}
    static string FormatTotal(long n){string[] u={"B","KB","MB","GB","TB"};double v=n;int i=0;while(v>=1024&&i<u.Length-1){v/=1024;i++;}return (i<2?v.ToString("0"):v.ToString("0.0"))+" "+u[i];}
  }

  sealed class DiskPanel:Panel { public long Total,Free;public string Root="";public DiskPanel(){DoubleBuffered=true;BackColor=Color.White;}protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var r=new Rectangle(25,10,190,190);using(var b=new SolidBrush(Color.FromArgb(220,231,239)))e.Graphics.FillEllipse(b,r);if(Total>0)using(var b=new SolidBrush(Color.FromArgb(39,125,161)))e.Graphics.FillPie(b,r,-90,(float)(360.0*(Total-Free)/Total));using(var b=new SolidBrush(Color.White))e.Graphics.FillEllipse(b,new Rectangle(70,55,100,100));using(var f=new Font("Segoe UI",14,FontStyle.Bold))using(var b=new SolidBrush(Color.Black)){var s=e.Graphics.MeasureString(Root,f);e.Graphics.DrawString(Root,f,b,120-s.Width/2,90);}} }
}
