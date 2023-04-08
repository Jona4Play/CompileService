namespace FP_API.CodeExecution;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Text;

/// <summary>
/// Bindings for container actions
/// </summary>
public class Container
{
	private LxdClient _client;

	private bool _isRunning;
	private Guid _id;
	private string _image;
	public bool Busy { get; private set; }
	public bool IsRunning
	{
		get
		{
			var response = GetContainerState().Result;
			Console.WriteLine(response);
			JObject.Parse(response.Content.ReadAsStringAsync().Result);
			return _isRunning;
		}
	}

	public Container(LxdClient client, string imageName)
	{
		_id = Guid.NewGuid();
		_image = imageName;
		_client = client;
		var response = CreateContainer().Result;

		_isRunning = response.IsSuccessStatusCode ? true : throw new Exception("Something went wrong while creation: " + response.Content.ReadAsStringAsync().Result);
	}
	public void Destroy()
	{
		_client.Containers.Remove(this);
	}

	public async Task<HttpResponseMessage> CreateContainer()
	{
		var request = new HttpRequestMessage(HttpMethod.Post, "/1.0/containers");
		request.Content = new StringContent($"{{\"name\":\"{_id}\",\"source\":{{\"type\":\"image\",\"alias\":\"{_image}\"}}}}", Encoding.UTF8, "application/json");
		return await _client.SendRequest(request);
	}

	/// <summary>
	/// Used to delete a container. Performs a run check before executing
	/// </summary>
	/// <param name="containerName">Name of the container to delete</param>
	/// <returns>Returns HTTP response</returns>
	public async Task<HttpResponseMessage> DeleteContainer()
	{
		var request = new HttpRequestMessage(HttpMethod.Delete, $"/1.0/containers/{_id}");
		return await _client.SendRequest(request);
	}

	/// <summary>
	/// Fetch runtime info of a container by name
	/// </summary>
	/// <param name="containerName">Name of the container</param>
	/// <returns></returns>
	private async Task<HttpResponseMessage> GetContainerState()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, $"/1.0/containers/{_id}/state");
		return await _client.SendRequest(request);
	}

	/// <summary>
	/// Used to stop an acitvely running container. This performs a run check before stopping
	/// </summary>
	/// <param name="containerName">Name of the container to stop</param>
	/// <returns>Returns true on successful stop, otherwise false</returns>
	public async Task<bool> StopContainer()
	{
		var state = await GetContainerState();
		Console.WriteLine(await state.Content.ReadAsStringAsync());
		if (state.StatusCode is not HttpStatusCode.OK)
			return false;

		var request = new HttpRequestMessage(HttpMethod.Put, $"1.0/containers/{_id}/state");
		var requestBody = new
		{
			action = "stop",
			timeout = 30
		};
		var json = JsonConvert.SerializeObject(requestBody);
		request.Content = new StringContent(json, Encoding.UTF8, "application/json");
		var reqResponse = await _client.SendRequest(request);
		if (reqResponse.StatusCode == HttpStatusCode.OK)
			return true;
		else
			Console.WriteLine(await reqResponse.Content.ReadAsStringAsync());
		return false;
	}

	public async Task<(string output, double cpuUsage, double memoryUsage)> RunCommand(string command)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "lxc",
			Arguments = $"exec {_id} -- {command}",
			RedirectStandardOutput = true
		};

		var process = new Process { StartInfo = startInfo };
		var stopwatch = new Stopwatch();

		stopwatch.Start();
		process.Start();
		var output = await process.StandardOutput.ReadToEndAsync();
		process.WaitForExit();
		stopwatch.Stop();

		var cpuUsage = process.TotalProcessorTime.TotalMilliseconds;
		var memoryUsage = process.WorkingSet64;

		return (output, cpuUsage, memoryUsage);
	}

	~Container()
	{
		var response = DeleteContainer().Result;
		if (!response.IsSuccessStatusCode)
			throw new Exception($"Something went wrong: {response.Content.ReadAsStringAsync().Result}");
	}
}
