using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace stocks.Hubs
{
    public class StocksHub : Hub
    {
        public async Task SubscribeCallerToIntradayQuotes(
            params string[] tickerSymbols) 
            => await tickerSymbols
                .ToAsyncEnumerable()
                .OfType<string>()
                .Select(t => t.Trim().ToUpperInvariant())
                .Where(t => intradayQuotesJsonByTickerSymbol.TryGetValue(t, out _))
                .ForEachAsync(async t =>
                {
                    addIntradayQuoteSubscription(t, Context.ConnectionId);
                    await Groups.AddToGroupAsync(Context.ConnectionId, t);

                    if (intradayQuotesJsonByTickerSymbol[t] is string)
                        await Clients.Caller.SendAsync(nameof(ReceiveIntradayQuotesJson), t, intradayQuotesJsonByTickerSymbol[t]);
                    else
                        ReceiveIntradayQuotesJson?.Invoke(t, intradayQuotesJsonByTickerSymbol[t] = await getIntradayQuotesJsonAsync(t));
                });
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            await Clients.User(Context.UserIdentifier).SendAsync("Connected");
        }
        public override async Task OnDisconnectedAsync(
            Exception exception)
        {
            await intradayQuotesJsonByTickerSymbol
                .Keys
                .ToAsyncEnumerable()
                .ForEachAsync(async t => await Groups.RemoveFromGroupAsync(Context.ConnectionId, t));
            await removeIntradayQuoteSubscriptionsForConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        static readonly HttpClient httpClient
            = new HttpClient();
        static readonly IDictionary<string, string> intradayQuotesJsonByTickerSymbol
            = new ConcurrentDictionary<string, string>(
                new Dictionary<string, string>
                {
                            { "AAPL" , default },
                            { "MSFT" , default },
                            { "TSLA" , default },
                            { "TWTR" , default }
                });

        static readonly IDictionary<string, IDictionary<string, byte>> intradayQuoteSubscriptionsByTickerSymbol
            = new ConcurrentDictionary<string, IDictionary<string, byte>>();

        static StocksHub()
        {
            const int requestIntervalInSeconds = 60;

            ManualResetEventSlim finishCurrentTasksWaitHandle = new ManualResetEventSlim();
            CancellationTokenSource updateIntradayQuotesCts = new CancellationTokenSource();
            CancellationToken updateIntradayQuotesCt = updateIntradayQuotesCts.Token;
            AppDomain.CurrentDomain.ProcessExit += (s, e) => finishUpdatingIntradayQuotes();
            new Thread(updateIntradayData)
            {
                IsBackground = true
            }.Start();

            void updateIntradayData()
            {
                while (!updateIntradayQuotesCt
                    .WaitHandle
                    .WaitOne(TimeSpan.FromSeconds(requestIntervalInSeconds)))
                    getTickerSymbolsWithIntradayQuoteSubscriptions()
                        .ForEach(async tickerSymbol
                            => ReceiveIntradayQuotesJson?.Invoke(
                                tickerSymbol,
                                intradayQuotesJsonByTickerSymbol[tickerSymbol] = await getIntradayQuotesJsonAsync(tickerSymbol)));

                finishCurrentTasksWaitHandle.Set();

            }
            void finishUpdatingIntradayQuotes()
            {
                updateIntradayQuotesCts.Cancel();
                finishCurrentTasksWaitHandle.Wait();
                httpClient.Dispose();
            }
        }

        public static event Action<string, string> ReceiveIntradayQuotesJson;

        private static void addIntradayQuoteSubscription(
            string tickerSymbol,
            string connectionId)
        {
            intradayQuoteSubscriptionsByTickerSymbol.TryAdd(tickerSymbol, new ConcurrentDictionary<string, byte>());
            intradayQuoteSubscriptionsByTickerSymbol[tickerSymbol].TryAdd(connectionId, default);
        }
        private static async Task removeIntradayQuoteSubscriptionsForConnection(
            string connectionId) 
            => await intradayQuoteSubscriptionsByTickerSymbol
                .Values
                .ToAsyncEnumerable()
                .Where(d => d.ContainsKey(connectionId))
                .ForEachAsync(d => d.Remove(connectionId));
        private static IAsyncEnumerable<string> getTickerSymbolsWithIntradayQuoteSubscriptions()
            => intradayQuoteSubscriptionsByTickerSymbol
                .ToAsyncEnumerable()
                .Where(d => d.Value?.Any() is true)
                .Select(d => d.Key);

        private static async Task<string> getIntradayQuotesJsonAsync(
            string tickerSymbol)
        {
            using (HttpResponseMessage response = await httpClient.GetAsync(
                $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={tickerSymbol}&interval=1min&apikey=4UJU16PLBC0WSPDB"))
            using (HttpContent content = response.Content)
                return await content.ReadAsStringAsync();
        }
    }
}
