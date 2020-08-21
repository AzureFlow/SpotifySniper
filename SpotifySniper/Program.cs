using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace SpotifySniper
{
	class Program
	{
		private static bool isDebug = false;

		private static readonly string LogFile = "Errors.txt";
		private static readonly string spotifyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify");
		private static readonly string configPath = Path.Combine(spotifyPath, "prefs");
		private static Dictionary<string, object> config = new Dictionary<string, object>();
		private static string proxyString = "127.0.0.1:8808@http";

		private static ProxyServer proxyServer = new ProxyServer();
		private static ExplicitProxyEndPoint explicitEndPoint;

		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();

		static void Main(string[] args)
		{
			Console.WriteLine("===== Welcome to SpotifySniper =====\n");
//#if(DEBUG)
			isDebug = GetConsoleWindow() != IntPtr.Zero;
//#endif
			AppDomain.CurrentDomain.UnhandledException += UnhandledDomainException;
			Application.ApplicationExit += new EventHandler(OnApplicationExit);

			if(System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
			{
				if(isDebug) Console.WriteLine("Already running!");
				return;
			}

			proxyServer.ExceptionFunc = ProxyException;

			//Locally trust root certificate used by this proxy
			proxyServer.CertificateManager.CreateRootCertificate(true);
			proxyServer.CertificateManager.TrustRootCertificate(true);

			//Under Mono only BouncyCastle will be supported
			//proxyServer.CertificateManager.CertificateEngine = Titanium.Web.Proxy.Network.CertificateEngine.BouncyCastle;

			//Register request callback
			proxyServer.BeforeRequest += OnRequest;
			//proxyServer.BeforeResponse += OnResponse;

			explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8808, decryptSsl: true) //Found out after 6 months this needs to be on even if you decide not to decrypt
			{
				// Use self-issued generic certificate on all https requests
				// Optimizes performance by not creating a certificate for each https-enabled domain
				// Useful when certificate trust is not required by proxy clients
				//GenericCertificate = new X509Certificate2(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "genericcert.pfx"), "totally secure password :)")
			};

			//Executed when a CONNECT request is received
			explicitEndPoint.BeforeTunnelConnectRequest += OnBeforeTunnelConnectRequest;

			//An explicit endpoint is where the client knows about the existence of a proxy so the client sends request in a proxy friendly manner
			proxyServer.AddEndPoint(explicitEndPoint);

			proxyServer.Start();

			//TODO: For mobile
			// Transparent endpoint is useful for reverse proxy (client is not aware of the existence of proxy)
			// A transparent endpoint usually requires a network router port forwarding HTTP(S) packets or DNS to send data to this endPoint
			/*proxyServer.AddEndPoint(new TransparentProxyEndPoint(IPAddress.Any, 8809, false)
			{
				// Generic Certificate hostname to use when SNI is disabled by client
				//GenericCertificateName = "google.com"
			});*/

			if(isDebug)
			{
				foreach(var endPoint in proxyServer.ProxyEndPoints)
				{
					Console.WriteLine("Listening on '{0}' endpoint at Ip {1} and port: {2} ", endPoint.GetType().Name, endPoint.IpAddress, endPoint.Port);
				}
			}

			SetupSpotifyProxy();

			if(!isDebug)
			{
				System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
			}
			else
			{
				Console.ReadKey();
				Quit();
			}
		}

		private static void SetupSpotifyProxy()
		{
			//Auto make Spotify use as proxy (via %AppData%\Spotify\prefs)
			//	network.proxy.addr="127.0.0.1:8808@http"
			//	network.proxy.mode=2
			if((string)GetConfigValue("network.proxy.addr") != proxyString)
			{
				SetConfigValue("network.proxy.addr", proxyString);

				Process[] processes = Process.GetProcessesByName("spotify");
				if(processes.Length > 0)
				{
					processes.ToList().ForEach(x => x.Kill());
					Process.Start(Path.Combine(spotifyPath, "Spotify.exe"));
				}
			}
		}

		private static async Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
		{
			string host = e.HttpClient.Request.RequestUri.Host;

			bool shouldDecrypt = host.Contains("spclient.wg.spotify.com") || host.Contains("googletagservices.com") || host.Contains("doubleclick.net") || host.Contains("rubiconproject.com") || host.Contains("pubmatic.com");
			if(isDebug) Console.WriteLine($"Before tunnel: {e.HttpClient.Request.RequestUri.Host} ({shouldDecrypt})");
			e.DecryptSsl = shouldDecrypt;

			await Task.CompletedTask;
		}

		//TODO: Modify response JSON (BeforeResponse) to avoid getting banned by Spotify (new TOS)
		private static async Task OnRequest(object sender, SessionEventArgs e)
		{
			if(isDebug) Console.WriteLine("OnRequest: " + e.HttpClient.Request.RequestUri);
			string host = e.HttpClient.Request.RequestUri.Host;

			if(host.Contains("googletagservices.com") || host.Contains("doubleclick.net") || host.Contains("rubiconproject.com") || host.Contains("pubmatic.com"))
			{
				e.Ok("<!DOCTYPE html><html><body>" +
					"<h1>Website Blocked</h1>" +
					"</body></html>");
			}

			if(host.Contains("spclient.wg.spotify.com"))
			{
				if(isDebug) Console.WriteLine(e.HttpClient.Request.RequestUri);
				if(e.HttpClient.Request.RequestUri.PathAndQuery.StartsWith("/ad"))
				{
					if(isDebug) Console.WriteLine("\tBLOCKED: " + e.HttpClient.Request.RequestUri);
					e.GenericResponse("HTTP/404 Not Found", HttpStatusCode.NotFound, null, true);
				}
			}
			
			await Task.CompletedTask;
		}

		private static Task OnResponse(object sender, SessionEventArgs e)
		{
			//if(isDebug) Console.WriteLine($"OnResponse: {e.HttpClient.Request.RequestUri}");
			return Task.CompletedTask;
		}

		private static void ProxyException(Exception e)
		{
			WriteError(e.ToString());
		}

		private static void UnhandledDomainException(object sender, UnhandledExceptionEventArgs e)
		{
			WriteError(e.ExceptionObject.ToString());
		}

		private static void WriteError(string text)
		{
			System.IO.File.AppendAllText(LogFile, text + "\n\n");
		}

		private static void OnApplicationExit(object sender, EventArgs e)
		{
			Quit();
		}

		private static void Quit()
		{
			if(isDebug) Console.WriteLine("Quiting...");

			//Unsubscribe & Quit
			explicitEndPoint.BeforeTunnelConnectRequest -= OnBeforeTunnelConnectRequest;
			proxyServer.BeforeRequest -= OnRequest;
			//proxyServer.BeforeResponse -= OnResponse;
			proxyServer.Stop();
			//Environment.Exit(0);
		}

		private static void InitConfig()
		{
			string[] configContent = File.ReadAllLines(configPath);
			foreach(var line in configContent)
			{
				string[] parts = line.Split(new char[] { '=' }, 2);
				string lineKey = parts[0];
				var lineValue = parts[1]; //TODO: Add type support

				config.Add(lineKey, lineValue);
			}
		}

		private static void SaveConfig()
		{
			string contents = string.Join("\n", config.Select(x => $"{x.Key}={x.Value}"));
			File.WriteAllText(configPath, contents);
			//Console.WriteLine(contents);
		}

		public static object GetConfigValue(string key)
		{
			if(config.Keys.Count < 1)
			{
				InitConfig();
			}

			if(!config.TryGetValue(key, out object value))
			{
				return null;
			}

			if(value.GetType() == typeof(string))
			{
				value = System.Text.RegularExpressions.Regex.Unescape(((string)value).Trim('"'));
			}

			return value;
		}

		public static void SetConfigValue(string key, object value)
		{
			if(config.Keys.Count < 1)
			{
				InitConfig();
			}

			if(value.GetType() == typeof(string))
			{
				value = $"\"{value}\"";
			}

			if(config.ContainsKey(key))
			{
				config[key] = value;
			}
			else
			{
				config.Add(key, value);
			}

			//Console.WriteLine(string.Join("\n", config.Select(x => $"{x.Key} => {x.Value}").ToArray()));
			SaveConfig();
		}
	}
}
