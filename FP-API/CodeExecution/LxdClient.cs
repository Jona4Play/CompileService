namespace FP_API.CodeExecution;
using System.Net;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections;

/// <summary>
/// Provides bindings for LXD API (v1.0)
/// </summary>
public class LxdClient : IDisposable
{
	private readonly HttpClient _client;
	private const string _defaultimage = "alpine/3.17";
	private readonly List<Container> _containers = new List<Container>();
	private List<LangInfo> _langs = new List<LangInfo>();
	public List<Container> Containers
	{
		get { return _containers; }
	}

	/// <summary>
	/// Connect to the socket of the local machine for operations
	/// </summary>
	/// <param name="socketPath">Path to unix.socket</param>
	public LxdClient(string socketPath)
	{
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
	}

	public void AddContainer()
	{
		Container container = new(this, _defaultimage);
		_containers.Add(container);
	}

	public void AddContainer(string imageAlias)
	{
		Container container = new(this, imageAlias);
		_containers.Add(container);
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
		return ((JArray)JObject.Parse(await response.Content.ReadAsStringAsync())["metadata"]).ToObject<string[]>().Select(x => x.Split("/").Last()).ToArray();
	}
	/// <summary>
	/// Fetch a list of supported languages from GitHub and parse it into the LangInfo struct
	/// </summary>
	private static List<LangInfo> InitLangs()
	{
		
	}

	public void Dispose()
	{
		foreach (var container in _containers)
		{
			container.Destroy();
		}
		_client.Dispose();
	}
}

public ref struct LangInfo
{
	ReadOnlySpan<char> language;
	ReadOnlySpan<char> compileCommand;
	ReadOnlySpan<char> fileEnding;
}