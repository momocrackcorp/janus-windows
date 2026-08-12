using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MigradorSeguro {
  static class Program {
    [STAThread] static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new MainForm());
    }
  }

  sealed class FolderItem {
    public string Label, RegistryName, DefaultName, Source;
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

    public MainForm() {
      Text = "Migrador seguro de carpetas de Windows"; Width = 1080; Height = 780;
      MinimumSize = new Size(920, 680); BackColor = Color.FromArgb(244,246,248);
      Font = new Font("Segoe UI", 9F); BuildFolders(); BuildUi();
      Shown += async (s,e) => await RefreshData();
    }

    void BuildFolders() {
      folders.Add(NewFolder("Escritorio", "Desktop", "Desktop"));
      folders.Add(NewFolder("Documentos", "Personal", "Documents"));
      folders.Add(NewFolder("Descargas", "{374DE290-123F-4565-9164-39C4925E467B}", "Downloads"));
      folders.Add(NewFolder("Imágenes", "My Pictures", "Pictures"));
      folders.Add(NewFolder("Música", "My Music", "Music"));
      folders.Add(NewFolder("Vídeos", "My Video", "Videos"));
    }
    FolderItem NewFolder(string label,string reg,string fallback) {
      string value = null;
      using (var key=Registry.CurrentUser.OpenSubKey(UserShell)) value=key==null?null:key.GetValue(reg,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
      if (String.IsNullOrWhiteSpace(value)) value=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),fallback);
      return new FolderItem {Label=label,RegistryName=reg,DefaultName=fallback,Source=Environment.ExpandEnvironmentVariables(value)};
    }

    void BuildUi() {
      var title=new Label{Text="Migrador seguro de carpetas",Font=new Font("Segoe UI",19,FontStyle.Bold),AutoSize=true,Location=new Point(22,16)};
      var subtitle=new Label{Text="Copia, verifica y redirige tus carpetas personales sin borrar los originales.",AutoSize=true,Location=new Point(25,54)};
      Controls.Add(title); Controls.Add(subtitle);
      var left=new Panel{BackColor=Color.White,Location=new Point(22,84),Size=new Size(650,585),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};
      var right=new Panel{BackColor=Color.White,Location=new Point(690,84),Size=new Size(350,585),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right};
      Controls.Add(left); Controls.Add(right);
      left.Controls.Add(Header("1. Destino",16,14));
      destination.SetBounds(18,48,500,27); destination.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; destination.TextChanged+=(s,e)=>UpdatePreview(); left.Controls.Add(destination);
      var browse=new Button{Text="Examinar…"}; browse.SetBounds(528,47,102,29); browse.Anchor=AnchorStyles.Top|AnchorStyles.Right; browse.Click+=(s,e)=>ChooseDestination(); left.Controls.Add(browse);
      drives.SetBounds(18,84,612,28); drives.DropDownStyle=ComboBoxStyle.DropDownList; drives.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; drives.SelectedIndexChanged+=(s,e)=>UpdateDrive(); left.Controls.Add(drives);
      left.Controls.Add(Header("2. Carpetas",16,124));
      int y=158;
      foreach(var f in folders) {
        f.Check=new CheckBox{Text=f.Label,Checked=true,Location=new Point(18,y),Width=120}; f.Check.CheckedChanged+=(s,e)=>UpdatePreview();
        f.SizeLabel=new Label{Text="Calculando…",Location=new Point(145,y+2),Width=85,TextAlign=ContentAlignment.MiddleRight};
        var path=new Label{Text=f.Source,ForeColor=Color.DimGray,Location=new Point(244,y+2),Width=386,AutoEllipsis=true,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
        left.Controls.Add(f.Check); left.Controls.Add(f.SizeLabel); left.Controls.Add(path); y+=32;
      }
      left.Controls.Add(Header("3. Vista previa origen → destino",16,358));
      preview.SetBounds(18,392,612,172); preview.Multiline=true; preview.ReadOnly=true; preview.ScrollBars=ScrollBars.Both; preview.WordWrap=false; preview.BackColor=Color.FromArgb(247,248,250); preview.Font=new Font("Consolas",8.5F); preview.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; left.Controls.Add(preview);
      right.Controls.Add(Header("Capacidad del disco",16,12)); disk.SetBounds(50,37,250,205); right.Controls.Add(disk);
      capacity.SetBounds(20,240,310,45); capacity.TextAlign=ContentAlignment.TopCenter; right.Controls.Add(capacity);
      required.SetBounds(22,290,306,66); required.Font=new Font("Segoe UI",10,FontStyle.Bold); right.Controls.Add(required);
      var protection=new Label{Text="Protecciones activas",Font=new Font("Segoe UI",10,FontStyle.Bold),Location=new Point(22,365),AutoSize=true}; right.Controls.Add(protection);
      var ptext=new Label{Text="• Nunca sobrescribe archivos\n• Nunca borra los originales\n• Verifica antes de cambiar Windows\n• Bloquea rutas críticas y falta de espacio\n• Crea un respaldo restaurable",Location=new Point(22,391),Size=new Size(306,82)}; right.Controls.Add(ptext);
      apply.Text="Revisar y aplicar migración"; apply.SetBounds(22,492,306,32); apply.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; apply.Click+=async(s,e)=>await ApplyMigration(); right.Controls.Add(apply);
      var restore=new Button{Text="Restaurar desde respaldo…"}; restore.SetBounds(22,532,306,32); restore.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; restore.Click+=(s,e)=>Restore(); right.Controls.Add(restore);
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
    void UpdatePreview() {
      long total=folders.Where(f=>f.Check.Checked).Sum(f=>f.Size); var lines=new List<string>();
      foreach(var f in folders.Where(x=>x.Check.Checked)) lines.Add(f.Source+Environment.NewLine+"  → "+(String.IsNullOrWhiteSpace(destination.Text)?"(elige destino)":Path.Combine(destination.Text,f.DefaultName)));
      preview.Text=String.Join(Environment.NewLine,lines); var selectedDrive=drives.SelectedItem as DriveInfo; long free=selectedDrive==null?0:selectedDrive.AvailableFreeSpace;
      bool ok=!String.IsNullOrWhiteSpace(destination.Text)&&folders.Any(f=>f.Check.Checked)&&free>=total+100L*1024*1024;
      required.Text="Datos a copiar: "+FormatBytes(total)+"\nLibre estimado después: "+FormatBytes(Math.Max(0,free-total))+"\n"+(ok?"✓ Espacio suficiente":"⚠ Revisa destino y espacio"); apply.Enabled=ok;
    }

    async Task ApplyMigration() {
      try {
        var selected=folders.Where(f=>f.Check.Checked).ToList(); string root=ValidateBase(destination.Text,selected.Select(f=>f.Source));
        foreach(var f in selected) { string dst=Path.Combine(root,f.DefaultName); if(Directory.Exists(dst)&&Directory.EnumerateFileSystemEntries(dst).Any()) throw new InvalidOperationException(dst+" ya contiene archivos. No se sobrescribirá."); }
        long needed=selected.Sum(f=>f.Size)+100L*1024*1024; var drive=new DriveInfo(Path.GetPathRoot(root)); if(drive.AvailableFreeSpace<needed) throw new InvalidOperationException("Espacio insuficiente. Faltan "+FormatBytes(needed-drive.AvailableFreeSpace)+".");
        string summary=String.Join("\n\n",selected.Select(f=>"• "+f.Label+": "+FormatBytes(f.Size)+"\n  "+f.Source+"\n  → "+Path.Combine(root,f.DefaultName)));
        if(MessageBox.Show("Se copiarán y verificarán estas carpetas:\n\n"+summary+"\n\nLos originales NO se borrarán. ¿Continuar?","Confirmación final",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        apply.Enabled=false; string backup=BackupRegistry(); status.Text="Respaldo guardado en "+backup;
        await Task.Run(()=> { foreach(var f in selected) CopyVerified(f.Source,Path.Combine(root,f.DefaultName),m=>BeginInvoke((Action)(()=>status.Text=m))); });
        var changed=new List<FolderItem>();
        try { foreach(var f in selected){SetRegistry(f.RegistryName,Path.Combine(root,f.DefaultName));changed.Add(f);} }
        catch { RestoreFile(backup,changed.Select(x=>x.Label)); throw; }
        status.Text="Migración completada correctamente.";
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
    static void CopyVerified(string src,string dst,Action<string> progress) { Directory.CreateDirectory(dst);foreach(var file in SafeFiles(src)){string rel=file.Substring(src.TrimEnd('\\').Length).TrimStart('\\');string target=Path.Combine(dst,rel);Directory.CreateDirectory(Path.GetDirectoryName(target));if(File.Exists(target))throw new IOException("Conflicto inesperado: "+target);File.Copy(file,target,false);if(new FileInfo(file).Length!=new FileInfo(target).Length)throw new IOException("Falló la verificación de "+Path.GetFileName(file));progress("Copiando "+Path.GetFileName(src)+": "+rel);}long a,b;int ac,bc;Stats(src,out a,out ac);Stats(dst,out b,out bc);if(a!=b||ac!=bc)throw new IOException("La copia no coincide con el origen; no se cambió Windows."); }
    string BackupRegistry() { var data=new Dictionary<string,object>();data["version"]=1;data["created"]=DateTime.Now.ToString("s");var values=new Dictionary<string,string>();foreach(var f in folders)values[f.Label]=f.Source;data["folders"]=values;string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Respaldos Migrador Seguro");Directory.CreateDirectory(dir);string path=Path.Combine(dir,"known-folders-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");File.WriteAllText(path,new JavaScriptSerializer().Serialize(data));return path; }
    static void SetRegistry(string name,string value) { using(var k=Registry.CurrentUser.OpenSubKey(UserShell,true))k.SetValue(name,value,RegistryValueKind.ExpandString);using(var k=Registry.CurrentUser.CreateSubKey(Shell))k.SetValue(name,Environment.ExpandEnvironmentVariables(value),RegistryValueKind.String); }
    void Restore() { using(var d=new OpenFileDialog{Filter="Respaldo JSON (*.json)|*.json",Title="Selecciona el respaldo"})if(d.ShowDialog()==DialogResult.OK&&MessageBox.Show("Se restaurarán las rutas. No se moverán ni borrarán archivos. ¿Continuar?","Restaurar",MessageBoxButtons.YesNo)==DialogResult.Yes){try{RestoreFile(d.FileName,null);if(MessageBox.Show("Rutas restauradas. ¿Reiniciar el Explorador?","Restaurado",MessageBoxButtons.YesNo)==DialogResult.Yes)RestartExplorer();}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo restaurar",MessageBoxButtons.OK,MessageBoxIcon.Error);}} }
    void RestoreFile(string path,IEnumerable<string> only) { var obj=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(path));var vals=(Dictionary<string,object>)obj["folders"];var set=only==null?null:new HashSet<string>(only);foreach(var f in folders)if((set==null||set.Contains(f.Label))&&vals.ContainsKey(f.Label))SetRegistry(f.RegistryName,Convert.ToString(vals[f.Label])); }
    static void RestartExplorer() { try{foreach(var p in Process.GetProcessesByName("explorer"))p.Kill();Process.Start("explorer.exe");}catch{} }
    static string FormatBytes(long n) { string[] u={"B","KB","MB","GB","TB"};double v=Math.Max(0,n);int i=0;while(v>=1024&&i<u.Length-1){v/=1024;i++;}return i<2?String.Format("{0:0} {1}",v,u[i]):String.Format("{0:0.0} {1}",v,u[i]); }
  }

  sealed class DiskPanel:Panel { public long Total,Free;public string Root="";public DiskPanel(){DoubleBuffered=true;BackColor=Color.White;}protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var r=new Rectangle(25,10,190,190);using(var b=new SolidBrush(Color.FromArgb(220,231,239)))e.Graphics.FillEllipse(b,r);if(Total>0)using(var b=new SolidBrush(Color.FromArgb(39,125,161)))e.Graphics.FillPie(b,r,-90,(float)(360.0*(Total-Free)/Total));using(var b=new SolidBrush(Color.White))e.Graphics.FillEllipse(b,new Rectangle(70,55,100,100));using(var f=new Font("Segoe UI",14,FontStyle.Bold))using(var b=new SolidBrush(Color.Black)){var s=e.Graphics.MeasureString(Root,f);e.Graphics.DrawString(Root,f,b,120-s.Width/2,90);}} }
}
