namespace FP_API.CodeExecution;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

/// <summary>
/// Bindings for container actions
/// </summary>
public class Container
{
	private LxdClient _lxdclient;

	private bool _isRunning;

	private string _id;

	private string _image;
	public bool Busy { get; private set; }
	private string BasePath { get; } = "/usercode";
	private CompileTask _task;
	private bool _isIdle;

	public bool IsRunning
	{
		get
		{
			var response = GetContainerState().Result;
			var result = JObject.Parse(response.Content.ReadAsStringAsync().Result);
			Console.WriteLine("Is running?: " + result);
			if (result.ToString().Contains("404"))
				return false;
			var isRunning = result["metadata"]!["status"]!.ToString() == "Running";
			Console.WriteLine(result["metadata"]!["status"]!.ToString());
			return isRunning;
		}
	}

	public Container(LxdClient client, string imageAlias, CompileTask task)
	{
		_id = "fv" + Guid.NewGuid().ToString();
		_image = imageAlias;
		_lxdclient = client;
		var response = CreateContainer().Result;
		_task = task;
		if (!response.IsSuccessStatusCode)
			throw new Exception("Something went wrong while creation: " + response.Content.ReadAsStringAsync().Result);
	}
	public Container(LxdClient client, string imageAlias)
	{
		_id = "fv" + Guid.NewGuid().ToString();
		_image = imageAlias;
		_lxdclient = client;
		var response = CreateContainer().Result;
		_isIdle = true;
		if (!response.IsSuccessStatusCode)
			throw new Exception("Something went wrong while creation: " + response.Content.ReadAsStringAsync().Result);
	}


	public async Task<HttpResponseMessage> CreateContainer()
	{
		var request = new HttpRequestMessage(HttpMethod.Post, "/1.0/instances");
		request.Content = new StringContent($"{{\"name\":\"{_id}\",\"source\":{{\"type\":\"image\",\"alias\":\"{_image}\"}}}}", Encoding.UTF8, "application/json");
		var response = await _lxdclient.SendRequest(request);
		var operationtype = JObject.Parse(await response.Content.ReadAsStringAsync())["status"];
		if (operationtype == null || !operationtype.ToString().StartsWith("Operation"))
		{
			return response;
		}
		while (true)
		{
			StartContainer();
			var containerState = await GetContainerState();
			System.Console.WriteLine(containerState);
			var containerJSON = JObject.Parse(await containerState.Content.ReadAsStringAsync());
			System.Console.WriteLine(containerJSON);
			try
			{
				var state = containerJSON["metadata"]!["status"]!.ToString();
				if (state == "Running")
				{
					return containerState;
				}
			}
			catch (Exception ex)
			{
				System.Console.WriteLine(ex.Message);
			}


			await Task.Delay(1000);
		}
	}
	public async Task<bool> Destroy()
	{
		var response = await DeleteContainer();
		if (!response.IsSuccessStatusCode)
			throw new Exception($"Something went wrong: {response.Content.ReadAsStringAsync().Result}");
		Console.WriteLine(await response.Content.ReadAsStringAsync());
		return true;
	}

	public void QueueTask(CompileTask compileTask)
	{
		_task = compileTask;
	}


	/// <summary>
	/// Starts up the container over the CLI
	/// </summary>
	public void StartContainer()
	{
		if (IsRunning)
			throw new Exception("Container is running");
		var startProcess = new Process();
		startProcess.StartInfo.FileName = "lxc";
		startProcess.StartInfo.Arguments = $"start {_id}";
		startProcess.Start();
		startProcess.WaitForExit();
	}

	/// <summary>
	/// Used to delete a container. Performs a run check before executing
	/// </summary>
	/// <param name="containerName">Name of the container to delete</param>
	/// <returns>Returns HTTP response</returns>
	private async Task<HttpResponseMessage> DeleteContainer()
	{
		if (IsRunning)
			StopContainer();
		var request = new HttpRequestMessage(HttpMethod.Delete, $"/1.0/instances/{_id}");
		return await _lxdclient.SendRequest(request);
	}

	/// <summary>
	/// Fetch runtime info of a container by name
	/// </summary>
	/// <param name="containerName">Name of the container</param>
	/// <returns></returns>
	private async Task<HttpResponseMessage> GetContainerState()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, $"/1.0/instances/{_id}/state");
		return await _lxdclient.SendRequest(request);
	}

	public void CreateFileInContainer(CompileTask compileTask)
	{
		var path = $"{BasePath}.{compileTask.LanguageInfo.ending}";
		var process = new Process();
		process.StartInfo.FileName = "lxc";
		process.StartInfo.Arguments = $"exec {_id} -- touch {path}";
		process.Start();
		process.WaitForExit();

		using (var streamWriter = new StreamWriter($"lxc:{_id}:{path}"))
		{
			streamWriter.Write(compileTask.code);
		}
	}

	/// <summary>
	/// Used to stop an acitvely running container. This performs a run check before stopping
	/// </summary>
	/// <param name="containerName">Name of the container to stop</param>
	/// <returns>Returns true on successful stop, otherwise false</returns>
	public void StopContainer()
	{
		if (!IsRunning)
			throw new Exception("Container is not running");
		var stopProcess = new Process();
		stopProcess.StartInfo.FileName = "lxc";
		stopProcess.StartInfo.Arguments = $"stop {_id}";
		stopProcess.Start();
		stopProcess.WaitForExit();
	}


	private async Task<bool> CreateFile()
	{
		try
		{
			var request = new HttpRequestMessage
			{
				Method = HttpMethod.Post,
				RequestUri = new Uri($"/1.0/instances/{_id}/console"),
				Headers =
			{
				{ "User-Agent", "LXD-Client" },
				{ "X-LXD-Interactive", "true" },
				{ "X-LXD-Type", "control" }
			},
				Content = new StringContent($"echo '{_task.code}' > /home/task.{_task.LanguageInfo.ending}\nexit\n")
				{
					Headers =
					{
						ContentType = MediaTypeHeaderValue.Parse("text/plain")
					}
				}
			};
			var response = await _lxdclient.SendRequest(request);
			return response.IsSuccessStatusCode;
		}
		catch (Exception ex)
		{
			throw new Exception("Something went wrong during the creation of the temp file:" + ex.Message);
		}
	}
	private async Task<bool> DeleteTask()
	{
		try
		{
			var request = new HttpRequestMessage
			{
				Method = HttpMethod.Post,
				RequestUri = new Uri($"/1.0/instances/{_id}/console"),
				Headers =
				{
					{ "User-Agent", "LXD-Client" },
					{ "X-LXD-Interactive", "true" },
					{ "X-LXD-Type", "control" }
				},
				Content = new StringContent($"rm /home/task.{_task.LanguageInfo.ending}\nexit\n")
				{
					Headers =
					{
						ContentType = MediaTypeHeaderValue.Parse("text/plain")
					}
				}
			};
			return (await _lxdclient.SendRequest(request)).IsSuccessStatusCode;
		}
		catch (Exception ex)
		{
			throw new Exception("Something went wrong during the deletion of the temp file:" + ex.Message);
		}
	}
	public async Task<CompileResult> Compile()
	{

		if (await CreateFile())
		{
			if (_isIdle)
			{
				await DeleteTask();
			}

		}
		throw new Exception("Something went wrong while compiling");
	}
}
