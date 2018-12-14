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
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AzureLogExporter
{
	public static class EhActivityLogsExt
	{
		[FunctionName("EhActivityLogsExt")]
		public static async Task Run(
			[EventHubTrigger("insights-operational-logs", Connection = "ActivityLogEventHubConnection")]string[] messages,
			IBinder blobFaultBinder,
			Binder queueFaultBinder,
			Microsoft.Extensions.Logging.ILogger log)
		{
			if (Config.GetBool(ConfigSettings.LogRawData))
			{
				string logMessages = string.Join("\r\n", messages);
				log.LogInformation($"Got:\r\n{logMessages}");
			}

			await Runner.Run<ActivityLogMessages>(
				messages,
				log,
				Runner.ReportFailuresToAzure<ActivityLogMessages>(blobFaultBinder, queueFaultBinder));
		}
	}
}
