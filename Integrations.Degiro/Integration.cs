using System;
using System.Text;
using System.Threading.Tasks;
using Integrations.Degiro.Models;
using Integrations.Degiro.Models.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Integrations.Degiro
{
    public interface IIntegration
    {
        Task<ICsv<CsvTransaction>> GetTransactionsAsync();
        Task<ICsv<CsvCashOperation>> GetCashOperationsAsync(int year);
    }

    internal class Integration : IIntegration
    {
        private readonly RestClient _client;
        private readonly RequestsConfiguration _configuration;
        private readonly string _jSessionId;
        private readonly string _accountId;

        public Integration(RequestsConfiguration configuration, string jSessionId)
        {
            _configuration = configuration;
            _jSessionId = jSessionId;
            _client = new RestClient();
            _accountId = GetAccountIdAsync().GetAwaiter().GetResult();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task<ICsv<CsvCashOperation>> GetCashOperationsAsync(int year)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year, 12, 31);

            return await DownloadCsvAsync<CsvCashOperation>(_configuration.CashOperationsUrl, startDate, endDate);
        }

        public async Task<ICsv<CsvTransaction>> GetTransactionsAsync()
        {
            //This date needs to be before 1st transaction.
            //Since Degiro was founded in 2008 I assume that there should be no transaction before that.
            var startDate = new DateTime(2007, 1, 1);
            var endDate = DateTime.Now.AddDays(1);

            return await DownloadCsvAsync<CsvTransaction>(_configuration.TransactionsUrl, startDate, endDate);
        }

        private async Task<ICsv<T>> DownloadCsvAsync<T>(string url, DateTime startDate, DateTime endDate)
        {
            const string dateFormat = "dd'%2F'MM'%2F'yyyy";

            var formattedUrl = string.Format(url, _accountId, _jSessionId, startDate.ToString(dateFormat), endDate.ToString(dateFormat));
            var request = new RestRequest(formattedUrl);

            var data = await _client.DownloadDataAsync(request);
            return new Csv<T>(Encoding.UTF8.GetString(data));
        }

        private async Task<string> GetAccountIdAsync()
        {
            var request = new RestRequest(string.Format(_configuration.AccountUrl, _jSessionId));
            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                throw new Exception($"Failed to get account ID: {response.ErrorMessage}");
            }

            return JObject.Parse(response.Content!)["data"]!["intAccount"]!.ToString();
        }
    }
}
