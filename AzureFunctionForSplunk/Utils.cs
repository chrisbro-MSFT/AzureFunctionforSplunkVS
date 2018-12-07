﻿//
// AzureFunctionForSplunkVS
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
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctionForSplunk
{
	public class TransmissionFaultMessage
	{
		public string id { get; set; }
		public string type { get; set; }
	}

	public class Utils
	{
		static string SplunkCertThumbprint { get; set; }

		public Utils()
		{
			SplunkCertThumbprint = GetEnvironmentVariable("splunkCertThumbprint");
		}

		public static string GetEnvironmentVariable(string name)
		{
			string result = System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
			if (result == null)
				return "";

			return result;
		}

		public class SingleHttpClientInstance
		{
			private static readonly HttpClient HttpClient;

			static SingleHttpClientInstance()
			{
				HttpClient = new HttpClient();
			}

			public static async Task<HttpResponseMessage> SendToSplunk(HttpRequestMessage req)
			{
				HttpResponseMessage response = await HttpClient.SendAsync(req);
				return response;
			}
		}

		private static bool ValidateMyCert(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslErr)
		{
			// if user has not configured a cert, anything goes
			if (string.IsNullOrWhiteSpace(SplunkCertThumbprint))
				return true;

			// if user has configured a cert, must match
			string thumbprint = cert.GetCertHashString();
			if (thumbprint == SplunkCertThumbprint)
				return true;

			return false;
		}

		public static async Task SendEvents(List<string> standardizedEvents, TraceWriter log)
		{
			string splunkAddress = Utils.GetEnvironmentVariable("splunkAddress");
			string splunkToken = Utils.GetEnvironmentVariable("splunkToken");
			if (splunkAddress.Length == 0 || splunkToken.Length == 0)
			{
				log.Error("Values for splunkAddress and splunkToken are required.");
				return;
			}

			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
			ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateMyCert);

			StringBuilder newClientContent = new StringBuilder();
			foreach (string item in standardizedEvents)
			{
				newClientContent.Append(item);
			}

			SingleHttpClientInstance client = new SingleHttpClientInstance();
			try
			{
				HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, splunkAddress);
				req.Headers.Accept.Clear();
				req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				req.Headers.Add("Authorization", "Splunk " + splunkToken);
				req.Content = new StringContent(newClientContent.ToString(), Encoding.UTF8, "application/json");
				HttpResponseMessage response = await SingleHttpClientInstance.SendToSplunk(req);
				if (response.StatusCode != HttpStatusCode.OK)
				{
					throw new System.Net.Http.HttpRequestException($"StatusCode from Splunk: {response.StatusCode}, and reason: {response.ReasonPhrase}");
				}
			}
			catch (System.Net.Http.HttpRequestException e)
			{
				throw new System.Net.Http.HttpRequestException("Sending to Splunk. Is Splunk service running?", e);
			}
			catch (Exception f)
			{
				throw new System.Exception("Sending to Splunk. Unplanned exception.", f);
			}
		}
	}
}
