using System.Security.Cryptography;
using System.Text;

namespace FP_API.CodeExecution
{
	public class SnippetGenerator
	{
		public CompileTask CompilationTask { get; private set; }
		private Random _random = new Random();
		public SnippetGenerator(CompileTask compileTask) 
		{
			CompilationTask = compileTask;
		}

		public string GenerateRandomString(int length)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			

			return new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[_random.Next(s.Length)]).ToArray());
		}

		/// <summary>
		/// Generate random values of a given type for testing cases
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="count"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public List<T> GenerateValues<T>(int count)
		{
			List<T> values = new List<T>();

			// Determine the type of value to generate
			Type valueType = typeof(T);

			// Determine how to generate the values based on the type
			Func<T> valueGenerator;

			switch (Type.GetTypeCode(valueType))
			{
				case TypeCode.Int32:
					valueGenerator = () => (T)(object)new Random().Next();
					break;
				case TypeCode.Double:
					valueGenerator = () => (T)(object)new Random().NextDouble();
					break;
				case TypeCode.String:
					valueGenerator = () => (T)(object)GenerateRandomString(10); // Replace 10 with desired string length
					break;
				default:
					throw new ArgumentException("Unsupported value type");
			}

			// Generate the specified number of values
			for (int i = 0; i < count; i++)
			{
				T value = valueGenerator();
				values.Add(value);
			}

			return values;
		}



		public void GenerateUserSnippet()
		{
			var usercode = CompilationTask.code.Content;
			StringBuilder sb = new();

			switch (CompilationTask.LanguageInfo.name) 
			{
				default:
					throw new Exception("Unknown language: " + CompilationTask.LanguageInfo.name);
				case "fsharp":
					sb.AppendLine("#r \"nuget: Newtonsoft.Json, 13.0.1\"");
					sb.AppendLine("open System");
					sb.AppendLine("open Newtonsoft.Json");
					sb.AppendLine("");
					sb.Append(usercode);
					sb.AppendLine();

					break;
				case "clojure":
					break;
				case "":
					break;
			}
			CompilationTask.code.Content = sb.ToString();
		}
	}
}
