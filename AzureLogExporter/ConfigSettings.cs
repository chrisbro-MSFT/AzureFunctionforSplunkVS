using System;
using System.Collections.Generic;

namespace AzureLogExporter
{
	public interface IConfigThingy
	{
		string GetValue(string name);
	}

	public static class Config
	{
		private static Lazy<IConfigThingy> s_settingsInstance = new Lazy<IConfigThingy>(DefaultConfig);
		private static IConfigThingy DefaultConfig() { return new ConfigFromEnvironmentVariables(); }

		public static string GetValue(string name)
		{
			return s_settingsInstance.Value.GetValue(name);
		}

		public static bool GetBool(string name)
		{
			string rawValue = s_settingsInstance.Value.GetValue(name);
			if (String.IsNullOrWhiteSpace(rawValue))
				return false;

			bool parsedValue;
			if (!Boolean.TryParse(rawValue, out parsedValue))
				return false;
			return parsedValue;
		}
	}

	/// <summary> The names of each config setting. </summary>
	public static class ConfigSettings
	{
		public static readonly string DestinationCertThumbprint = "destinationCertThumbprint";
		public static readonly string DestinationAddress = "destinationAddress";
		public static readonly string DestinationToken = "destinationToken";
		public static readonly string LogRawData = "logRawData";
	}

	public class ConfigFromEnvironmentVariables : IConfigThingy
	{
		public string GetValue(string name)
		{
			return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ?? "";
		}
	}

	public class ConfigHardcoded : IConfigThingy
	{
		protected Dictionary<string, string> _settings = new Dictionary<string, string>();

		public ConfigHardcoded(Dictionary<string, string> settings)
		{
			_settings = settings;
		}

		public string GetValue(string name)
		{
			if (_settings.TryGetValue(name, out string value))
				return value;
			return "";
		}
	}
}