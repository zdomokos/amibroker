﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AmiBroker.Plugin.Models;

namespace AmiBroker.Plugin.Providers.Stooq
{
    internal class StooqDataSource : DataSource
    {
        private readonly Regex _dateRegex = new Regex(@"^\d+$");
        private readonly string _databasePath;
        public StooqDataSource(string databasePath, IntPtr mainWnd) : base(databasePath, mainWnd)
        {
            _databasePath = databasePath;
        }

        public override Quotation[] GetQuotes(string ticker, Periodicity periodicity, int limit,
            Quotation[] existingQuotes)
        {
            var result = GetQotations(ticker);

            if (result.Count > limit)
            {
                result = result.GetRange(result.Count - limit, limit);
            }

            return result.ToArray();
        }

        public List<Quotation> GetQotations(string ticker)
        {
            var list = new List<Quotation>();
            var fileContent = new StooqDataLoader(ticker, _databasePath).LoadFile();
            foreach (var line in fileContent)
            {
                var value = line.Split(',');
                if (!_dateRegex.Match(value[0].Replace("-", "")).Success) continue;

                var quotation = new Quotation
                {
                    DateTime = new AmiDate(
                        DateTime.ParseExact(value[0].Replace("-", ""), "yyyyMMdd", CultureInfo.InvariantCulture),
                        true),
                    Open = Convert.ToSingle(value[1], CultureInfo.InstalledUICulture),
                    High = Convert.ToSingle(value[2], CultureInfo.InstalledUICulture),
                    Low = Convert.ToSingle(value[3], CultureInfo.InstalledUICulture),
                    Price = Convert.ToSingle(value[4], CultureInfo.InstalledUICulture),
                    // not always volume is given in the file
                    Volume =
                        value.Length < 6 ? 0 : Convert.ToSingle(value[5], CultureInfo.InstalledUICulture)
                };

                list.Add(quotation);
            }
            return list;
        }

    }
}