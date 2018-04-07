using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Ionic.Zip;

namespace AutoBackupToOneDrive
{
	class Program
	{
		static int oneSecond = 1000;
		static int oneMinute = 60 * oneSecond;
		static int oneHour = 60 * oneMinute;

		static int backupInterval = oneHour;

		static string sourcePath = "";
		static string archiveName = "";
		static string targetPath = "";

		static void Main(string[] args)
		{
			Console.WriteLine($"AutoBackup into folder");
			Console.WriteLine($"Version {Assembly.GetEntryAssembly().GetName().Version}");

			if (args.Count() < 3)
			{
				Console.WriteLine("Make sure that you add require: {sourcePath}, {archiveName}, {targetFolder}, optional: {backupInterval}");
				return;
			}

			InitParams(args);

			ThreadPool.QueueUserWorkItem(async (ctx) =>
			{
				try
				{
					await Run();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
			});

			while (true)
			{
				Thread.Sleep(oneHour);
			}
		}

		static async Task Run()
		{
			CopyToDestination().Wait();

			var startTime = DateTime.UtcNow;

			while (true)
			{
				if (startTime.Day != DateTime.UtcNow.Day)
				{
					startTime = DateTime.UtcNow;
					await CopyToDestination();
				}

				Thread.Sleep(backupInterval);
			}
		}

		static void InitParams(string[] args)
		{
			sourcePath = args[0];
			archiveName = args[1];
			targetPath = args[2];
			if (args.Length == 4)
			{
				backupInterval = Convert.ToInt16(args[3]) * oneMinute;
			}
		}

		static async Task CopyToDestination()
		{
			var targetName = $"{archiveName}-{DateTime.UtcNow.ToShortDateString()}.zip".Replace("/", ".");
			var targetFileName = Path.Combine(targetPath, targetName);

			var buffer = await ArhiveStreamAsync(sourcePath, $"{archiveName}.zip");

			using (var fileStream = new FileStream(targetFileName, FileMode.Create))
			{
				fileStream.Write(buffer, 0, buffer.Length);
				fileStream.Close();
			}

			Console.WriteLine($"Archive succeeded {DateTime.UtcNow}");
		}

		static async Task<byte[]> ArhiveStreamAsync(string path, string arhiveFileName)
		{
			string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
			if (File.Exists(arhiveFileName)) File.Delete(arhiveFileName);

			using (var zip = new ZipFile(arhiveFileName))
			{
				foreach (var file in files)
				{
					var fileInfo = new FileInfo(file);
					var directory = fileInfo.DirectoryName.Substring(path.Length);
					if (!String.IsNullOrWhiteSpace(directory)) directory = directory.Replace("\\", "");
					zip.AddFile(file, directory);
				}

				zip.Save();

				using (var memory = new MemoryStream())
				using (FileStream currentFile = new FileStream(arhiveFileName, FileMode.Open))
				{
					await currentFile.CopyToAsync(memory);

					currentFile.Close();

					return memory.ToArray();
				}
			}
		}
	}
}