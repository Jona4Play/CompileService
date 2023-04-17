namespace FP_API.CodeExecution;
using System.Net;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Text.Json;
using System.Net.Http.Headers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides bindings for LXD API (v1.0)
/// </summary>
public class LxdClient : IDisposable
{
	private readonly HttpClient _client;
	private const string _defaultimage = "fvirt-1.0.0";
	private List<Container> _containers = new List<Container>();
	private List<Language> Languages { get; set; }
	private const byte _standByContainers = 3;
	public List<Container> Containers
	{
		get { return _containers; }
		set { _containers = value; }
	}

	/// <summary>
	/// Connect to the socket of the local machine for operations
	/// </summary>
	/// <param name="socketPath">Path to unix.socket</param>
	public LxdClient(string socketPath)
	{
		var langstask = InitLangs();
		var handler = new SocketsHttpHandler
		{
			ConnectCallback = async (context, cancellationToken) =>
			{
				var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
				await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
				return new NetworkStream(socket);
			}
		};
		_client = new HttpClient(handler)
		{
			BaseAddress = new Uri("http://localhost/")
		};
		Languages = langstask.Result;
		List<Task<Container>> ctasks = new();
		for (int i = 0; i < _standByContainers; i++)
		{
			ctasks.Add(Task.Run(AddIdleContainer));
		}
		Containers = Task.WhenAll(ctasks).Result.ToList();
	}

	public Container AddIdleContainer()
	{
		return new Container(this, _defaultimage);
	}

	public Container AddContainer(CompileTask ct)
	{
		return new Container(this, _defaultimage, ct);
	}

	public async Task<CompileResult> DelegateCompilation(CompileTask ct)
	{
		foreach (var container in Containers)
		{
			if (!container.Busy)
			{
				container.QueueTask(ct);
				return await container.Compile();
			}
		}
		var newcontainer = AddContainer(ct);
		Containers.Add(newcontainer);
		return await newcontainer.Compile();
	}

	/// <summary>
	/// Wrapper method for socket connection
	/// </summary>
	/// <param name="request">Request</param>
	/// <returns>HttpResponse of LXD API</returns>
	public async Task<HttpResponseMessage> SendRequest(HttpRequestMessage request)
	{
		return await _client.SendAsync(request);
	}


	/// <summary>
	/// Get the name of all active Containers
	/// </summary>
	/// <returns>An array of containers GUIDs</returns>
	public async Task<string[]> GetContainers()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, $"/1.0/instances");
		var response = await SendRequest(request);
		var containers = ((JArray)JObject.Parse(await response.Content.ReadAsStringAsync())["metadata"]!).ToObject<string[]>()!.Select(x => x.Split("/").Last()).ToArray();
		if (containers.Length > 0)
			return containers;
		else
			Console.WriteLine("There are no containers");
		return containers;
	}
	/// <summary>
	/// Fetch a list of supported languages from GitHub and parse it into the LangInfo struct
	/// </summary>
	private async Task<List<Language>> InitLangs()
	{
		using (var webclient = new HttpClient())
		{

			var file = await webclient.GetStringAsync("https://raw.githubusercontent.com/Jona4Play/CompileService/master/FP-API/languages.json");
			var langinfo = JsonConvert.DeserializeObject<Languages>(file)!.languages;

			if (langinfo != null)
				return langinfo;
			throw new Exception("Something went wrong while reading from GitHub: LangInfo is null");
		}
	}

	public async void Dispose()
	{
		try
		{
			Console.WriteLine("Started Clearup of containers");
			var destructiontasks = _containers.Select(x => x.Destroy());
			var results = await Task.WhenAll(destructiontasks);
			if (!results.All(b => b))
				throw new Exception("Clearup of one or more containers failed");
			_containers.Clear();
			_client.Dispose();
		}
		catch(Exception ex)
		{
			throw new Exception("Clearup went wrong: " + ex.Message);
		}
	}
}
public struct CompileTask
{
	public Code code;
	public Language LanguageInfo;
	public TypeCode problemType;
}
public struct CompileResult
{
	public Language langInfo;
	public double totalTime;
	public double executionTime;
	public double spinUpTime;
	public double cpuCycles;
	public double memoryUsage;

	public CompileResult(Language language, double totalTime, double executionTime, double spinUpTime, double cpuCycles, double memoryUsage)
	{
		langInfo = language;
		this.totalTime = totalTime;
		this.executionTime = executionTime;
		this.spinUpTime = spinUpTime;
		this.cpuCycles = cpuCycles;
		this.memoryUsage = memoryUsage;
	}
}
public class Language
{
	public string? name { get; set; }
	public string? ending { get; set; }
	public string? compileCommand { get; set; }
}

public class Languages
{
	public List<Language>? languages { get; set; }
}

public class Code
{
	public string? Content { get; set; }
	public string? FunctionName { get; set; }
}