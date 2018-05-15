using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace stocks.Hubs
{
    public class StocksHub : Hub
    {
        static readonly HttpClient client
            = new HttpClient();

        public async Task GetIntradayStockData(
            string tickerSymbol)
        {
            string normalizedTickerSymbol = tickerSymbol
                ?.ToUpper()
                ?.Trim();

            if (supportedTickerSymbols.Contains(normalizedTickerSymbol))
                using (HttpResponseMessage response = await client.GetAsync(
                    $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={normalizedTickerSymbol}&interval=1min&apikey=4UJU16PLBC0WSPDB"))
                using (HttpContent content = response.Content)
                        await Clients.All.SendAsync(
                            "ReceiveStockIntradayData",
                            normalizedTickerSymbol, 
                            await content.ReadAsStringAsync());
        }

        readonly ISet<string> supportedTickerSymbols
            = new HashSet<string>
            {
                "MSFT",
                "AAPL"
            };

        static StocksHub() 
            => AppDomain.CurrentDomain.ProcessExit += 
                (s, e) => client.Dispose();
    }
}
