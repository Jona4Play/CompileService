namespace FP_API.CodeExecution;

public class CoreHandler
{

	public static async Task Main(string[] args)
	{
		using(LxdClient client = new("/var/lib/lxd/unix.socket"))
		{
			client.AddContainer();
			var response = await client.GetContainers();
			foreach (var container in response)
			{
				Console.WriteLine(container);
			}
		}
	}
}