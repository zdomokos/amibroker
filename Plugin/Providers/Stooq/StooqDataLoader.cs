﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace AmiBroker.Plugin.Providers.Stooq
{
    internal class StooqDataLoader
    {
        private const string DownloadLastRun = "LAST_DOWNLOAD_RUN";
        private const string LastEntryInFile = "LAST_ENTRY_IN_FILE";
        private const string DefaultStartDate = "1970-01-01 00:00";
        private const string Url = @"http://stooq.pl/q/d/l/?s={0}&d1={1}&d2={2}&i=d";
        private readonly Dictionary<string, string> _config;
        private readonly string _configFile;
        private readonly string _file;
        private readonly string _ticker;

        public StooqDataLoader(string ticker, string databasePath)
        {
            _ticker = ticker;
            _file = databasePath + @"\" + ticker + ".csv";
            _configFile = databasePath + @"\" + ticker + ".config";
            _config = LoadConfig();
        }

        public List<string> LoadFile()
        {
            var result = LoadLocalFile();
            if (!IsRefreshNeeded()) return result;

            var remoteFile = LoadRemoteFile();
            result = Merge(result, remoteFile);
            SaveFile(result);
            SaveConfig();

            return result;
        }

        private List<string> Merge(List<string> one, List<string> two)
        {
            // first line of the stooq file has to be skipped
            if (two.Count < 2) return one;
            if (one.Count < 2) return two;

            var firstDateInSecond = Convert.ToInt64(two[1].Split(',')[0].Replace("-", ""));

            // find the last entry in one which is older then then first entry in second list
            int i;
            for (i = one.Count - 1; i > one.Count - two.Count; i--)
            {
                if (String.IsNullOrEmpty(one[i])) continue;

                var twoDate = Convert.ToInt64(one[i].Split(',')[0].Replace("-", ""));
                if (twoDate < firstDateInSecond)
                {
                    break;
                }
            }

            var result = one.GetRange(0, i + 1);
            result.AddRange(two.GetRange(1, two.Count - 1));

            return result;
        }

        private List<string> LoadRemoteFile()
        {
            try
            {
                var startDate = _config.GetValue(LastEntryInFile, "19700101").Replace("-", "");
                var endDate = DateTime.Now.ToString("yyyyMMdd");
                var url = String.Format(Url, _ticker, startDate, endDate);

                var request = WebRequest.Create(url);
                var response = request.GetResponse();

                var dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.
                var reader = new StreamReader(dataStream);
                // Read the content.
                var responseFromServer = reader.ReadToEnd();

                return Regex.Split(responseFromServer, @"\r\n").ToList();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return new List<string>();
        }

        private List<string> LoadLocalFile()
        {
            if (File.Exists(_file))
            {
                var fileContent = File.ReadAllLines(_file).ToList();

                if (fileContent.Count > 0)
                {
                    for (var i = fileContent.Count - 1; i > 0; i--)
                    {
                        if (!String.IsNullOrEmpty(fileContent[i]))
                        {
                            var lastLine = fileContent[i].Split(',')[0];
                            _config.AddOrReplaceValue(LastEntryInFile, lastLine);
                            break;
                        }
                    }
                }
                return fileContent;
            }
            return new List<string>();
        }

        private Boolean IsRefreshNeeded()
        {
            return DateTime.Now.Date > DateTime.ParseExact(
                _config.GetValue(DownloadLastRun, DefaultStartDate),
                "yyyy-MM-dd HH:mm",
                CultureInfo.InstalledUICulture
                ).Date;
        }

        private void SaveFile(List<string> fileContent)
        {
            _config.AddOrReplaceValue(DownloadLastRun, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            File.WriteAllLines(_file, fileContent);
        }

        private Dictionary<string, string> LoadConfig()
        {
            var data = new Dictionary<string, string>();
            if (File.Exists(_configFile))
            {
                foreach (var row in File.ReadAllLines(_configFile))
                    data.Add(row.Split('=')[0], String.Join("=", row.Split('=').Skip(1).ToArray()));
            }

            return data;
        }

        private void SaveConfig()
        {
            var configLines = _config.Select(keyValue => keyValue.Key + "=" + keyValue.Value).ToList();
            File.WriteAllLines(_configFile, configLines);
        }
    }

    public static class LocalExtentions
    {
        public static string GetValue(this Dictionary<string, string> dictionary, string key, string defaultValue)
        {
            string value;
            if (!dictionary.TryGetValue(key, out value))
            {
                value = defaultValue;
            }

            return value;
        }

        public static void AddOrReplaceValue(this Dictionary<string, string> dictionary, string key, string value)
        {
            if (dictionary.ContainsKey(key))
            {
                dictionary.Remove(key);
            }

            dictionary.Add(key, value);
        }
    }
}