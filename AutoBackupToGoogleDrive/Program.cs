using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

using Ionic.Zip;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using GoogleData = Google.Apis.Drive.v3.Data;

namespace AutoBackup
{
	class Program
	{
		// If modifying these scopes, delete your previously saved credentials
		// at ~/.credentials/drive-dotnet-quickstart.json
		static string[] Scopes = { DriveService.Scope.Drive };
		static string ApplicationName = "Backup websites To OneDrive";
		static DriveService driveService;
		static string driveFolder = "0B1b2eLQWiRYRZnFmRkkwSmJTNU0";
		static string backupPath = "";
		static string backupName = "";


		static int oneSecond = 1000;
		static int oneMinute = 60 * oneSecond;
		static int oneHour = 60 * oneMinute;

		static int backupInterval = oneHour;

		static void Main(string[] args)
		{
			Console.WriteLine($"AutoBackup into GoogleDriver");
			Console.WriteLine($"Version {Assembly.GetEntryAssembly().GetName().Version}");

			if (args.Count() != 2)
			{
				Console.WriteLine("Make sure that you add require: {backupPath}, {backupName}, optional: {backupInterval}, {driveFolderId}");
				return;
			}

			Init(args).Wait();

			Upload().Wait();
			ListFolder().Wait();

			var startTime = DateTime.UtcNow;

			while (true)
			{
				if (startTime.Day != DateTime.UtcNow.Day)
				{
					startTime = DateTime.UtcNow;
					Upload().Wait();
					ListFolder().Wait();
				}

				Thread.Sleep(oneHour);
			}
		}

		static async Task Init(string[] args)
		{
			backupPath = args[0];
			backupName = args[1];
			if (args.Count() == 3)
			{
				backupInterval = Convert.ToInt16(args[2]) * oneMinute;
			}
			if (args.Count() == 4) driveFolder = args[2];

			UserCredential credential;

			using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
			{
				string credPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

				credPath = Path.Combine(credPath, ".credentials/drive-dotnet-quickstart.json");

				credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
					GoogleClientSecrets.Load(stream).Secrets,
					Scopes,
					"user",
					CancellationToken.None,
					new FileDataStore(credPath, true));

				//Console.WriteLine("Credential file saved to: " + credPath);
			}

			driveService = new DriveService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName,
			});
		}

		static async Task ListFolder()
		{
			// Define parameters of request.
			var listRequest = driveService.Files.List();
			listRequest.PageSize = 10;
			listRequest.Fields = "nextPageToken, files(id, name)";
			listRequest.Q = $"'{driveFolder}' in parents";

			var fileList = await listRequest.ExecuteAsync();

			Console.WriteLine("Files:");

			if (fileList?.Files != null && fileList.Files.Any())
			{
				foreach (var file in fileList.Files)
				{
					Console.WriteLine("{0} ({1})", file.Name, file.Id);
				}
			}
			else
			{
				Console.WriteLine("No files found.");
			}
		}

		static async Task Upload()
		{
			using (var stream = await ArhiveStreamAsync(backupPath, $"{backupName}.zip"))
			{
				var fileResource = new GoogleData.File()
				{
					Name = $"{backupName}-{DateTime.UtcNow.ToShortDateString()}.zip",
					Parents = new List<string>() { driveFolder },
					Description = "",
					MimeType = "application/zip",
				};

				var file = driveService.Files.Create(fileResource, stream, "application/zip");

				var response = file.Upload();

				Console.WriteLine(response.Status);

				Console.WriteLine(response.Exception?.Message);
			}
		}

		static async Task<Stream> ArhiveStreamAsync(string path, string arhiveFileName)
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
			}

			var memory = new MemoryStream();

			using (FileStream currentFile = new FileStream(arhiveFileName, FileMode.Open))
			{
				await currentFile.CopyToAsync(memory);

				currentFile.Close();
			}

			return memory;
		}
	}
}