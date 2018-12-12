using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace AzureLogExporter
{
	public static class FaultProcessor
	{
		[FunctionName("FaultProcessor")]
		public static async Task Run(
			[QueueTrigger("transmission-faults", Connection = "AzureWebJobsStorage")]string fault,
			IBinder blobFaultBinder,
			ILogger log)
		{
			TransmissionFaultMessage faultData = JsonConvert.DeserializeObject<TransmissionFaultMessage>(fault);

			CloudBlockBlob blobReader = await blobFaultBinder.BindAsync<CloudBlockBlob>(
					new BlobAttribute($"transmission-faults/{faultData.id}", FileAccess.ReadWrite));

			string json = await blobReader.DownloadTextAsync();

			try
			{
				List<string> faultMessages = await Task<List<string>>.Factory.StartNew(() => JsonConvert.DeserializeObject<List<string>>(json));
				await Utils.SendEvents(faultMessages, log);
			}
			catch
			{
				log.LogError($"FaultProcessor failed to send: {faultData.id}");
				return;
			}

			await blobReader.DeleteAsync();

			log.LogInformation($"C# Queue trigger function processed: {faultData.id}");
		}
	}
}
