using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Integrations.Degiro.Models;

namespace Integrations.Degiro
{
    public interface ICsv<T>
    {
        List<T> GetRows();
    }

    public class Csv<T> : ICsv<T>
    {
        private readonly List<T> _records;
        private static readonly CultureInfo PolishNumberCulture = CultureInfo.GetCultureInfo("pl-PL");
        private static readonly Regex GuidRegex = new Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

        public Csv(string csv)
        {
            _records = typeof(T) switch
            {
                var type when type == typeof(CsvTransaction) => (List<T>)(object)ParseTransactions(csv),
                var type when type == typeof(CsvCashOperation) => (List<T>)(object)ParseCashOperations(csv),
                _ => ParseGeneric(csv)
            };
        }

        public List<T> GetRows()
        {
            return _records;
        }

        private static List<T> ParseGeneric(string csv)
        {
            using var stringReader = new StringReader(csv);
            using var csvReader = CreateCsvReader(stringReader, DetectDelimiter(csv));

            return csvReader.GetRecords<T>().ToList();
        }

        private static List<CsvTransaction> ParseTransactions(string csv)
        {
            using var stringReader = new StringReader(csv);
            using var csvReader = CreateCsvReader(stringReader, DetectDelimiter(csv));

            csvReader.Read();
            csvReader.ReadHeader();

            var header = csvReader.HeaderRecord ?? Array.Empty<string>();
            var isNewFormat = header.Length == 18;
            var records = new List<CsvTransaction>();

            while (csvReader.Read())
            {
                var transaction = isNewFormat
                    ? ParseTransactionNewFormat(csvReader)
                    : ParseTransactionOldFormat(csvReader);

                // DEGIRO sometimes emits artifact rows without a transaction identifier.
                if (!string.IsNullOrWhiteSpace(transaction.TransactionId))
                    records.Add(transaction);
            }

            return records;
        }

        private static CsvTransaction ParseTransactionOldFormat(CsvReader csvReader)
        {
            return new CsvTransaction
            {
                Date = Field(csvReader, 0),
                Time = Field(csvReader, 1),
                InstrumentName = Field(csvReader, 2),
                Isin = Field(csvReader, 3),
                StockExchangeName = Field(csvReader, 4),
                StockLocation = Field(csvReader, 5),
                Quantity = ParseNullableInt(Field(csvReader, 6)),
                UnitPrice = ParseNullableDecimal(Field(csvReader, 7)),
                UnitPriceCurrency = Field(csvReader, 8),
                LocalValue = ParseNullableDecimal(Field(csvReader, 9)),
                LocalCurrency = Field(csvReader, 10),
                DegiroAmount = ParseNullableDecimal(Field(csvReader, 11)),
                DegiroCurrency = Field(csvReader, 12),
                ExchangeRate = ParseNullableDecimal(Field(csvReader, 13)),
                FeeAmount = ParseNullableDecimal(Field(csvReader, 14)),
                FeeCurrency = Field(csvReader, 15),
                TotalAmount = ParseNullableDecimal(Field(csvReader, 16)),
                TotalAmountCurrency = Field(csvReader, 17),
                TransactionId = Field(csvReader, 18),
            };
        }

        private static CsvTransaction ParseTransactionNewFormat(CsvReader csvReader)
        {
            var priceIndex = HeaderIndex(csvReader, "Price");
            var localValueIndex = HeaderIndex(csvReader, "Local value");
            var localCurrencyIndex = HeaderIndex(csvReader, "LocalCurrency");
            var valueEurIndex = HeaderIndex(csvReader, "Value EUR");
            var exchangeRateIndex = HeaderIndex(csvReader, "Exchange rate");
            var autoFxFeeIndex = HeaderIndex(csvReader, "AutoFX Fee");
            var transactionFeeIndex = HeaderIndex(csvReader, "Transaction and/or third party fees EUR");
            var totalAmountIndex = HeaderIndex(csvReader, "Total EUR");

            var autoFxFee = ParseNullableDecimal(Field(csvReader, autoFxFeeIndex)) ?? 0m;
            var transactionFee = transactionFeeIndex >= 0
                ? ParseNullableDecimal(Field(csvReader, transactionFeeIndex)) ?? 0m
                : 0m;
            var feeAmount = autoFxFee + transactionFee;

            return new CsvTransaction
            {
                Date = Field(csvReader, HeaderIndex(csvReader, "Date")),
                Time = Field(csvReader, HeaderIndex(csvReader, "Time")),
                InstrumentName = Field(csvReader, HeaderIndex(csvReader, "Product")),
                Isin = Field(csvReader, HeaderIndex(csvReader, "ISIN")),
                StockExchangeName = Field(csvReader, HeaderIndex(csvReader, "Reference exchange")),
                StockLocation = Field(csvReader, HeaderIndex(csvReader, "Venue")),
                Quantity = ParseNullableInt(Field(csvReader, HeaderIndex(csvReader, "Quantity"))),
                UnitPrice = ParseNullableDecimal(Field(csvReader, priceIndex)),
                UnitPriceCurrency = Field(csvReader, priceIndex + 1),
                LocalValue = ParseNullableDecimal(Field(csvReader, localValueIndex)),
                LocalCurrency = localCurrencyIndex >= 0
                    ? Field(csvReader, localCurrencyIndex)
                    : Field(csvReader, localValueIndex + 1),
                DegiroAmount = ParseNullableDecimal(Field(csvReader, valueEurIndex)),
                DegiroCurrency = "EUR",
                ExchangeRate = ParseNullableDecimal(Field(csvReader, exchangeRateIndex)),
                FeeAmount = feeAmount,
                FeeCurrency = feeAmount != 0m ? "EUR" : string.Empty,
                TotalAmount = ParseNullableDecimal(Field(csvReader, totalAmountIndex)),
                TotalAmountCurrency = "EUR",
                TransactionId = SelectTransactionId(csvReader),
            };
        }

        private static List<CsvCashOperation> ParseCashOperations(string csv)
        {
            using var stringReader = new StringReader(csv);
            using var csvReader = CreateCsvReader(stringReader, DetectDelimiter(csv));

            csvReader.Read();
            csvReader.ReadHeader();

            var header = csvReader.HeaderRecord ?? Array.Empty<string>();
            var isNewFormat = header.Length > 2 &&
                              string.Equals(header[2], "Value date", StringComparison.OrdinalIgnoreCase);

            var records = new List<CsvCashOperation>();

            while (csvReader.Read())
            {
                records.Add(isNewFormat
                    ? ParseCashOperationNewFormat(csvReader)
                    : ParseCashOperationOldFormat(csvReader));
            }

            return records;
        }

        private static CsvCashOperation ParseCashOperationOldFormat(CsvReader csvReader)
        {
            return new CsvCashOperation
            {
                ExecutionDate = Field(csvReader, 0),
                ExecutionTime = Field(csvReader, 1),
                Date = Field(csvReader, 2),
                Product = Field(csvReader, 3),
                Isin = Field(csvReader, 4),
                Description = Field(csvReader, 5),
                ExchangeRate = ParseNullableDecimal(Field(csvReader, 6)),
                ChangeCurrency = Field(csvReader, 7),
                ChangeAmount = ParseNullableDecimal(Field(csvReader, 8)),
                BalanceCurrency = Field(csvReader, 9),
                BalanceAmount = ParseNullableDecimal(Field(csvReader, 10)),
                TransactionId = Field(csvReader, 11),
            };
        }

        private static CsvCashOperation ParseCashOperationNewFormat(CsvReader csvReader)
        {
            return new CsvCashOperation
            {
                ExecutionDate = Field(csvReader, 0),
                ExecutionTime = Field(csvReader, 1),
                Date = Field(csvReader, 2),
                Product = Field(csvReader, 3),
                Isin = Field(csvReader, 4),
                Description = Field(csvReader, 5),
                ExchangeRate = ParseNullableDecimal(Field(csvReader, 6)),
                ChangeCurrency = Field(csvReader, 7),
                ChangeAmount = ParseNullableDecimal(Field(csvReader, 8)),
                BalanceCurrency = Field(csvReader, 9),
                BalanceAmount = ParseNullableDecimal(Field(csvReader, 10)),
                TransactionId = Field(csvReader, 11),
            };
        }

        private static CsvReader CreateCsvReader(StringReader stringReader, string delimiter)
        {
            return new CsvReader(stringReader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter,
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null,
            });
        }

        private static string DetectDelimiter(string csv)
        {
            using var stringReader = new StringReader(csv);
            var firstLine = stringReader.ReadLine() ?? string.Empty;

            var commaCount = firstLine.Count(_ => _ == ',');
            var semicolonCount = firstLine.Count(_ => _ == ';');

            return semicolonCount > commaCount ? ";" : ",";
        }

        private static string SelectTransactionId(CsvReader csvReader)
        {
            for (var i = 0; csvReader.TryGetField(i, out string field); i++)
            {
                var candidate = (field ?? string.Empty).Trim();
                if (GuidRegex.IsMatch(candidate))
                    return candidate;
            }

            var orderIdIndex = HeaderIndex(csvReader, "Order ID");
            return orderIdIndex >= 0 ? Field(csvReader, orderIdIndex) : string.Empty;
        }

        private static int HeaderIndex(CsvReader csvReader, string headerName)
        {
            var header = csvReader.HeaderRecord ?? Array.Empty<string>();
            for (var i = 0; i < header.Length; i++)
            {
                if (string.Equals(header[i], headerName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static string Field(CsvReader csvReader, int index)
        {
            return csvReader.TryGetField(index, out string field)
                ? (field ?? string.Empty).Trim()
                : string.Empty;
        }

        private static int? ParseNullableInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("\u00A0", string.Empty);

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
                return integerValue;

            var culture = normalized.Contains(',') ? PolishNumberCulture : CultureInfo.InvariantCulture;
            if (decimal.TryParse(normalized, NumberStyles.Number, culture, out var decimalValue) &&
                decimal.Truncate(decimalValue) == decimalValue)
                return (int)decimalValue;

            throw new FormatException($"'{value}' is not a supported DEGIRO quantity format.");
        }

        private static decimal? ParseNullableDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var culture = value.Contains(',') ? PolishNumberCulture : CultureInfo.InvariantCulture;
            return decimal.Parse(value, NumberStyles.Number, culture);
        }
    }
}
