using System.Text;
using System.Text.Json;
namespace AxiWidgets.Core;
public sealed record SshConnection {
 public Guid Id {get;set;}=Guid.NewGuid();
 public string Name {get;set;}="New server";
 public string Host {get;set;}="";
 public int Port {get;set;}=22;
 public string User {get;set;}="";
 public string Key {get;set;}="";
 public string Options {get;set;}="";
 public string Alias {get;set;}="";
}
public sealed class SshStore {
 readonly string path;
 public List<SshConnection> Items {get;private set;}=[];
 public event Action? Changed;
 public SshStore(string path){this.path=path;if(File.Exists(path))Items=JsonSerializer.Deserialize<List<SshConnection>>(File.ReadAllText(path))??[];}
 public void Save(IEnumerable<SshConnection> items){var next=items.Select(x=>x with{}).ToList();foreach(var c in next)SshConfig.Validate(c);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next));File.Move(path+".tmp",path,true);Items=next;Changed?.Invoke();}
 public static int Merge(List<SshConnection> items,IEnumerable<SshConnection> imported){int added=0;foreach(var c in imported){if(items.Any(x=>(c.Alias.Length>0&&x.Alias.Length>0?x.Alias.Equals(c.Alias,StringComparison.OrdinalIgnoreCase):x.Host.Equals(c.Host,StringComparison.OrdinalIgnoreCase)&&x.Port==c.Port&&x.User==c.User&&x.Key==c.Key&&x.Options==c.Options)))continue;items.Add(c);added++;}return added;}
}
public static class SshConfig {
 public static List<string> Split(string value){var result=new List<string>();var part=new StringBuilder();char quote='\0';foreach(char c in value){if(c==quote){quote='\0';continue;}if(quote=='\0'&&(c=='"'||c=='\'')){quote=c;continue;}if(char.IsWhiteSpace(c)&&quote=='\0'){if(part.Length>0){result.Add(part.ToString());part.Clear();}}else part.Append(c);}if(quote!='\0')throw new FormatException("Unclosed quote in SSH options.");if(part.Length>0)result.Add(part.ToString());return result;}
 public static void Validate(SshConnection c){if(string.IsNullOrWhiteSpace(c.Name)||string.IsNullOrWhiteSpace(c.Host)||c.Host.StartsWith('-')||c.Host.Any(char.IsWhiteSpace)||c.Host.Any(char.IsControl))throw new FormatException("Enter a name and a valid host.");if(c.Port<1||c.Port>65535)throw new FormatException("Port must be between 1 and 65535.");if(c.User.Any(char.IsControl)||c.Key.Any(char.IsControl))throw new FormatException("Invalid user or key path.");Split(c.Options);}
 // Reads config text only. Match exec and ProxyCommand are never executed on import.
 public static List<SshConnection> Import(string path){
  var lines=new List<string>();var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  void Read(string file,int depth){file=Path.GetFullPath(file);if(depth>8||!visited.Add(file)||!File.Exists(file))return;if(new FileInfo(file).Length>1048576)throw new IOException("SSH config is too large.");foreach(string line in File.ReadLines(file)){var t=line.Trim();if(t.StartsWith("Include ",StringComparison.OrdinalIgnoreCase)){foreach(var inc in Split(t[8..])){string expanded=inc.StartsWith("~/")?Path.Combine(home,inc[2..]):Path.IsPathRooted(inc)?inc:Path.Combine(Path.GetDirectoryName(path)!,inc);string dir=Path.GetDirectoryName(expanded)!;if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir,Path.GetFileName(expanded)).Order())Read(f,depth+1);}}else lines.Add(t);}}
  Read(path,0);var aliases=new List<string>();foreach(var line in lines){var pair=Pair(line);if(pair.Key=="host")foreach(var alias in Split(pair.Value))if(!alias.Contains('*')&&!alias.Contains('?')&&!alias.StartsWith('!')&&!aliases.Contains(alias,StringComparer.OrdinalIgnoreCase))aliases.Add(alias);}
  var result=new List<SshConnection>();foreach(string alias in aliases){bool matches=true;var values=new Dictionary<string,string>();foreach(var line in lines){var p=Pair(line);if(p.Key=="host"){var patterns=Split(p.Value);bool Match(string pattern)=>System.Text.RegularExpressions.Regex.IsMatch(alias,"^"+System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*",".*").Replace("\\?",".")+"$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);matches=patterns.Any(x=>!x.StartsWith('!')&&Match(x))&&!patterns.Any(x=>x.StartsWith('!')&&Match(x[1..]));}else if(p.Key=="match")matches=false;else if(matches&&!values.ContainsKey(p.Key))values[p.Key]=p.Value.Trim('"');}
   var c=new SshConnection{Name=alias,Alias=alias,Host=values.GetValueOrDefault("hostname",alias).Replace("%h",alias),User=values.GetValueOrDefault("user",""),Key=values.GetValueOrDefault("identityfile","")};if(int.TryParse(values.GetValueOrDefault("port","22"),out int port))c.Port=port;Validate(c);result.Add(c);
  }return result;
 }
 static KeyValuePair<string,string> Pair(string line){char quote='\0';for(int n=0;n<line.Length;n++){char c=line[n];if(c==quote)quote='\0';else if(quote=='\0'&&(c=='"'||c=='\''))quote=c;else if(quote=='\0'&&c=='#'){line=line[..n].TrimEnd();break;}}if(line.Length==0||line.StartsWith('#'))return new("","");int i=line.IndexOfAny([' ','\t','=']);if(i<0)return new(line.ToLowerInvariant(),"");return new(line[..i].ToLowerInvariant(),line[(i+1)..].TrimStart(' ','\t','='));}
 public static List<string> Arguments(SshConnection c){Validate(c);var args=new List<string>{"-p",c.Port.ToString()};if(c.User.Length>0){args.Add("-l");args.Add(c.User);}if(c.Key.Length>0){args.Add("-i");args.Add(c.Key.StartsWith("~/")?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),c.Key[2..]):c.Key);}if(c.Alias.Length>0){args.Add("-o");args.Add("HostName="+c.Host);}args.AddRange(Split(c.Options));args.Add(c.Alias.Length>0?c.Alias:c.Host);return args;}
 public static string PowerShellCommand(SshConnection c)=>"& ssh.exe "+string.Join(" ",Arguments(c).Select(x=>"'"+x.Replace("'","''")+"'"));
}
