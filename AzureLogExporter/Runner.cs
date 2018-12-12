//
// AzureLogExporterVS
//
// Copyright (c) Microsoft Corporation
//
// All rights reserved. 
//
// MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the ""Software""), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is furnished 
// to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all 
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS 
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR 
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using Newtonsoft.Json;

namespace AzureLogExporter
{
	public delegate Task ReportFailuresDelegate<T>(ILogger log, List<string> parsedMessages, Exception exEmit);

	public static class Runner
	{
		internal static ReportFailuresDelegate<T> ReportFailuresToAzure<T>(IBinder blobFaultBinder, Binder queueFaultBinder)
		{
			return (log, parsedMessage,exEmit) => LogFailuresToAzureStuff<T>(blobFaultBinder, queueFaultBinder, log, parsedMessage, exEmit);
		}

		public static async Task Run<T>(
			string[] messages,
			ILogger log,
			ReportFailuresDelegate<T> reportFailuresDelegate = null)
		{
			AzMonMessages azMonMsgs = (AzMonMessages)Activator.CreateInstance(typeof(T), log);
			List<string> parsedMessages = null;
			try
			{
				parsedMessages = azMonMsgs.DecomposeIncomingBatch(messages);
			}
			catch (Exception)
			{
				throw;
			}

			if (parsedMessages?.Count == 0)
			{
				log.LogError($"Trigger function processed a batch of {messages.Length} messages but couldn't parse any of them into objects");
				return;
			}

			if (parsedMessages.Count != messages.Length)
			{
				log.LogWarning($"Trigger function processed a batch of {messages.Length} messages but only successfully parsed {parsedMessages.Count}");
			}

			// TODO: Augment the messages with workload-specific data

			try
			{
				await Utils.SendEvents(parsedMessages, log);
			}
			catch (Exception exEmit)
			{
				if (reportFailuresDelegate != null)
				{
					await reportFailuresDelegate(log, parsedMessages, exEmit);
				}
				else
				{
					log.LogError($"Failed to send events.  No fancy logger configured.  Enjoy this exception: {exEmit}");
				}
			}

			log.LogInformation($"C# Event Hub trigger function processed a batch of messages: {messages.Length}");
		}

		private static async Task LogFailuresToAzureStuff<T>(IBinder blobFaultBinder, Binder queueFaultBinder, ILogger log, List<string> parsedMessages, Exception exEmit)
		{
			string id = Guid.NewGuid().ToString();
			log.LogError($"Failed to write the fault queue: {id}. {exEmit}");

			try
			{
				CloudBlockBlob blobWriter = await blobFaultBinder.BindAsync<CloudBlockBlob>(new BlobAttribute($"transmission-faults/{id}", FileAccess.ReadWrite));

				string json = await Task<string>.Factory.StartNew(() => JsonConvert.SerializeObject(parsedMessages));
				await blobWriter.UploadTextAsync(json);
			}
			catch (Exception exFaultBlob)
			{
				log.LogError($"Failed to write the fault blob: {id}. {exFaultBlob}");
			}

			try
			{
				TransmissionFaultMessage qMsg = new TransmissionFaultMessage { id = id, type = typeof(T).ToString() };
				string qMsgJson = JsonConvert.SerializeObject(qMsg);

				CloudQueue queueWriter = await queueFaultBinder.BindAsync<CloudQueue>(new QueueAttribute("transmission-faults"));
				await queueWriter.AddMessageAsync(new CloudQueueMessage(qMsgJson));
			}
			catch (Exception exFaultQueue)
			{
				log.LogError($"Failed to write the fault queue: {id}. {exFaultQueue}");
			}
		}
	}
}
