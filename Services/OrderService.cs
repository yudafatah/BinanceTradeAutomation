using BinanceTradeBot.Contracts;
using BinanceTradeBot.Entities;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BinanceTradeBot.Services
{
    public class OrderService : IOrderService
    {
        private readonly BinanceCredential _cred;

        public OrderService(IOptions<BinanceCredential> cred)
        {
            _cred = cred.Value;
        }

        public async Task SpotOrderWithSL(string symbol, decimal quantity, decimal price, decimal slPercent)
        {
            const string side = "BUY";
            const string type = "STOP_LOSS_LIMIT";
            const string timeInForce = "GTC";

            decimal stopPrice = price - (price * slPercent);

            // Generate a timestamp
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // Construct the query string
            string queryString = $"symbol={symbol}&quantity={quantity}&price={price}&stopPrice={stopPrice}&side={side}&type={type}&timeInForce={timeInForce}&timestamp={timestamp}";

            // Sign the query string with the secret key
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_cred.SecretKey));
            byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            string signature = BitConverter.ToString(signatureBytes).Replace("-", "");

            // Construct the request URL
            string url = $"https://api.binance.com/api/v3/order?{queryString}&signature={signature}";

            // Construct the request headers
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-MBX-APIKEY", _cred.ApiKey);

            // Send the request
            var response = await client.PostAsync(url, null);
        }

        public async Task<string> SpotOrder(string symbol, decimal quantity)
        {
            string side = "BUY";
            string type = "MARKET";
            string timeInForce = "GTC";

            // Set the API endpoint and request method
            string endpoint = "/api/v3/order";
            Method method = Method.Post;

            // Set the query string parameters
            var parameters = new RestRequest(endpoint, method);
            parameters.AddHeader("X-MBX-APIKEY", _cred.ApiKey);
            parameters.AddParameter("symbol", symbol);
            parameters.AddParameter("side", side);
            parameters.AddParameter("type", type);
            //parameters.AddParameter("timeInForce", timeInForce);
            parameters.AddParameter("quoteOrderQty", quantity);
            parameters.AddParameter("recvWindow", 60000);
            //parameters.AddParameter("price", price);

            // Set the signature for the request
            string timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            string queryString = @"symbol=" + symbol + "&side=" + side + "&type=" + type + "&quoteOrderQty=" + quantity + "&recvWindow=" + 60000;
            string signature = CreateSignature(_cred.SecretKey, queryString + "&timestamp=" + timestamp);
            parameters.AddParameter("timestamp", timestamp);
            parameters.AddParameter("signature", signature);

            //HttpClient hc = new HttpClient();
            //var r = await hc.GetAsync("https://api.binance.com/api/v3/time");
            //var t = await r.Content.ReadAsStringAsync();

            // Send the request to Binance API
            var client = new RestClient("https://api.binance.com");
            var response = await client.ExecuteAsync(parameters);

            return response.Content ?? "";
        }

        public async Task<string> SpotSell(string symbol)
        {
            // Get available balance
            decimal quantity = await GetAvailableBalance(_cred.ApiKey, _cred.SecretKey, symbol); // Get available balance

            if (quantity == 0 || quantity < 0) return "quantity 0";

            decimal stepSize = GetStepSize(symbol);

            decimal gap = quantity % stepSize;

            quantity -= gap;

            // Step 1: Calculate timestamp
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Step 2: Create signature
            string message = $"symbol={symbol}&side=SELL&type=MARKET&timestamp={timestamp}&quantity={quantity}&recvWindow=60000";
            string signature = CreateSignature(_cred.SecretKey, message);

            // Step 3: Create request URL
            string url = $"https://api.binance.com/api/v3/order?symbol={symbol}&side=SELL&type=MARKET&timestamp={timestamp}&quantity={quantity}&recvWindow=60000&signature={signature}";

            // Step 4: Create request headers
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("X-MBX-APIKEY", _cred.ApiKey);

            // Step 5: Send request
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            foreach (KeyValuePair<string, string> header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
            HttpResponseMessage response = await client.SendAsync(request);

            // Step 6: Handle response
            return await response.Content.ReadAsStringAsync();
        }

        // Helper method to get the available balance of a specific symbol
        static async Task<decimal> GetAvailableBalance(string apiKey, string secretKey, string symbol)
        {
            string baseUrl = "https://api.binance.com";
            string endpoint = "/api/v3/account";
            string timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            string signature = CreateSignature(secretKey, $"timestamp={timestamp}&recvWindow=60000");
            string url = $"{baseUrl}{endpoint}?timestamp={timestamp}&recvWindow=60000&signature={signature}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic parsedResponse = JsonConvert.DeserializeObject(jsonResponse);
                    foreach (var balance in parsedResponse.balances)
                    {
                        if (balance.asset == symbol.Replace("USDT", ""))
                        {
                            decimal availableBalance = decimal.Parse(balance.free.ToString());
                            return availableBalance;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Error getting account info: {response.StatusCode}");
                }
            }

            return 0;
        }

        private static string CreateSignature(string apiSecret, string message)
        {
            var keyBytes = Encoding.UTF8.GetBytes(apiSecret);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyBytes))
            {
                var hash = hmacsha256.ComputeHash(messageBytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private static decimal GetStepSize(string symbol)
        {
            return symbol switch
            {
                "XRPUSDT" => 1m,
                "BTCUSDT" => 0.00001m,
                "ETHUSDT" => 0.0001m,
                "CKBUSDT" => 0.00001m,
                "LTCUSDT" => 0.1m,
                "ADAUSDT" => 0.001m,
                "AXSUSDT" => 0.01m,
                "SHIBUSDT" => 100000m,
                "BNBUSDT" => 0.1m,
                "MATICUSDT" => 0.001m,
                "DOGEUSDT" => 1m,
                "SOLUSDT" => 0.01m,
                "STXUSDT" => 0.1m,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
