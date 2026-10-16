using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace JanusDock;
public class DockItem { public string Name {get;set;}=""; public string Path {get;set;}=""; }
public class DockSettings { public List<DockItem> Items {get;set;}=new(); }
internal static class Program {
 [STAThread] static void Main() {
  using var mutex=new Mutex(true,@"Local\JANUS_Dock_OnlyOne",out var first);
  if(!first) return;
  ApplicationConfiguration.Initialize(); Application.Run(new Dock());
 }
}
public class Dock:Form {
 const int ABM_NEW=0,ABM_REMOVE=1,ABM_QUERYPOS=2,ABM_SETPOS=3,ABE_RIGHT=2;
 [StructLayout(LayoutKind.Sequential)] struct Rect { public int left,top,right,bottom; }
 [StructLayout(LayoutKind.Sequential)] struct AppBar {
  public int cbSize; public IntPtr hWnd; public int uCallbackMessage,uEdge; public Rect rc; public IntPtr lParam;
 }
 [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(int cmd,ref AppBar data);
 readonly string config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"JANUS","Dock","config.json");
 readonly FlowLayoutPanel panel=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(4)};
 DockSettings settings=new();
 bool registered=false;
 AppBar Data()=>new(){cbSize=Marshal.SizeOf<AppBar>(),hWnd=Handle,uCallbackMessage=0x8000+417,uEdge=ABE_RIGHT};
 public Dock() {
  try {settings=JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(config))??new();} catch{}
  Text="JANUS Dock"; Width=90; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
  BackColor=Color.FromArgb(36,48,72); ForeColor=Color.White; AllowDrop=true;
  var title=new Label {Text="JANUS",Dock=DockStyle.Top,Height=42,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",10,FontStyle.Bold)};
  var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=78,FlowDirection=FlowDirection.TopDown,WrapContents=false};
  var add=new Button{Text="+ Agregar",Width=78,Height=30};
  add.Click+=(_,_)=>AddDialog();
  var close=new Button{Text="Cerrar",Width=78,Height=30};
  close.Click+=(_,_)=>Close();
  bottom.Controls.Add(add);bottom.Controls.Add(close);
  Controls.Add(panel);Controls.Add(bottom);Controls.Add(title);
  DragEnter+=EnterDrag;DragDrop+=DropFiles; panel.AllowDrop=true;panel.DragEnter+=EnterDrag;panel.DragDrop+=DropFiles;
  Render();
 }
 void Store(){Directory.CreateDirectory(Path.GetDirectoryName(config)!);var temp=config+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,config,true);}
 void Add(string path){
  if(!File.Exists(path)&&!Directory.Exists(path)&&!(Uri.TryCreate(path,UriKind.Absolute,out var uri)&&(uri.Scheme=="https"||uri.Scheme=="http")))return;
  var name=Path.GetFileNameWithoutExtension(path.TrimEnd('\\'));
  settings.Items.Add(new DockItem{Name=string.IsNullOrWhiteSpace(name)?path:name,Path=path});
  Store();Render();
 }
 void AddDialog(){
  using var f=new OpenFileDialog{Title="Elige un archivo, programa o acceso directo",Filter="Todos los archivos|*.*"};
  if(f.ShowDialog(this)==DialogResult.OK)Add(f.FileName);
  else {
   var text=Microsoft.VisualBasic.Interaction.InputBox("Para carpetas o sitios web, pega la ruta o URL:","JANUS Dock","");
   if(!string.IsNullOrWhiteSpace(text)) Add(text.Trim());
  }
 }
 void EnterDrag(object? s,DragEventArgs e)=>e.Effect=e.Data?.GetDataPresent(DataFormats.FileDrop)==true?DragDropEffects.Copy:DragDropEffects.None;
 void DropFiles(object? s,DragEventArgs e) {
  if(e.Data?.GetData(DataFormats.FileDrop) is string[] paths)foreach(var path in paths)Add(path);
 }
 void Render(){
  panel.SuspendLayout();panel.Controls.Clear();
  foreach(var item in settings.Items.ToList()){
   var btn=new Button{Text=item.Name,Width=72,Height=66,AutoEllipsis=true,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(62,79,113),ForeColor=Color.White,Font=new Font("Segoe UI",8)};
   btn.FlatAppearance.BorderSize=0; btn.Click+=(_,_)=>Open(item);
   var context=new ContextMenuStrip();
   context.Items.Add("Abrir",null,(_,_)=>Open(item));
   context.Items.Add("Subir",null,(_,_)=>Move(item,-1));
   context.Items.Add("Bajar",null,(_,_)=>Move(item,1));
   context.Items.Add("Quitar de la barra",null,(_,_)=>{settings.Items.Remove(item);Store();Render();});
   btn.ContextMenuStrip=context;panel.Controls.Add(btn);
  }
  panel.ResumeLayout();
 }
 void Move(DockItem item,int delta){
  int i=settings.Items.IndexOf(item), n=i+delta;
  if(i<0||n<0||n>=settings.Items.Count)return;
  settings.Items.RemoveAt(i);settings.Items.Insert(n,item);Store();Render();
 }
 void Open(DockItem item){
  try{Process.Start(new ProcessStartInfo(item.Path){UseShellExecute=true});}
  catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo abrir",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }
 void Position(){
  if(!registered)return;
  var bounds=Screen.PrimaryScreen?.Bounds??Screen.FromControl(this).Bounds;
  var d=Data();d.rc=new Rect{left=bounds.Right-Width,top=bounds.Top,right=bounds.Right,bottom=bounds.Bottom};
  SHAppBarMessage(ABM_QUERYPOS,ref d); d.rc.left=d.rc.right-Width;SHAppBarMessage(ABM_SETPOS,ref d);
  Bounds=new Rectangle(d.rc.left,d.rc.top,Width,d.rc.bottom-d.rc.top);
 }
 protected override void OnHandleCreated(EventArgs e){
  base.OnHandleCreated(e);var d=Data();registered=SHAppBarMessage(ABM_NEW,ref d)!=IntPtr.Zero;
  if(registered)Position();else {var b=Screen.PrimaryScreen!.Bounds;Bounds=new Rectangle(b.Right-Width,b.Top,Width,b.Height);}
 }
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(registered&&(m.Msg==0x8000+417||m.Msg==0x007e))Position();}
 protected override void OnFormClosing(FormClosingEventArgs e){
  if(registered){var d=Data();SHAppBarMessage(ABM_REMOVE,ref d);registered=false;}
  base.OnFormClosing(e);
 }
}
