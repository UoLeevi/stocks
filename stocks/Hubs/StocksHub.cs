using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using stocks.Models;
using stocks.Models.Translators.AlphaVantage;

namespace stocks.Hubs
{
    // TODO
    // use cancellationTokens
    public class StocksHub : Hub
    {
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
            await removeAllIntradayQuoteSubscriptionsForConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
        public async Task SubscribeCallerToQuotes(
        params string[] tickerSymbols)
        => await tickerSymbols
            .ToAsyncEnumerable()
            .OfType<string>()
            .Select(t => (Instrument)t)
            .Where(t => intradayQuotesJsonByTickerSymbol.TryGetValue(t, out _))
            .ForEachAsync(async t =>
            {
                addQuotesSubscription(t, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, t);

                if (quotesByInstrument.TryGetValue(t, out ImmutableSortedSet<Quote> quotes))
                    await Clients.Caller.SendAsync(nameof(ReceiveQuotes), t, quotes.ToArray());
                else
                    await updateAndSendIntradayQuotes(t);
            });
        public async Task UnsubscribeCallerFromIntradayQuotes(
            params string[] tickerSymbols)
            => await tickerSymbols
                .ToAsyncEnumerable()
                .OfType<string>()
                .Select(t => t.Trim().ToUpperInvariant())
                .Where(t => intradayQuotesJsonByTickerSymbol.TryGetValue(t, out _))
                .ForEachAsync(t => removeIntradayQuoteSubscription(t, Context.ConnectionId));
        public async Task SendSupportedTickerSymbolsToCaller()
            => await Clients.Caller.SendAsync(
                "ReceiveSupportedTickerSymbols",
                intradayQuotesJsonByTickerSymbol.Keys.ToArray());


        static string apiKey;

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

        static readonly IDictionary<Instrument, ImmutableSortedSet<Quote>> quotesByInstrument
            = new ConcurrentDictionary<Instrument, ImmutableSortedSet<Quote>>();

        static readonly IDictionary<string, IDictionary<string, byte>> intradayQuoteSubscriptionsByTickerSymbol
            = new ConcurrentDictionary<string, IDictionary<string, byte>>();

        public static void Start(
            string apiKey)
        {
            StocksHub.apiKey = apiKey;

            const int requestIntervalInSeconds = 60;

            ManualResetEventSlim mre = new ManualResetEventSlim();
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            AppDomain.CurrentDomain.ProcessExit += (s, e) => finishUpdatingIntradayQuotes();
            new Thread(updateIntradayData)
            {
                IsBackground = true
            }.Start();

            void updateIntradayData()
            {
                while (!ct
                    .WaitHandle
                    .WaitOne(TimeSpan.FromSeconds(requestIntervalInSeconds)))
                    getTickerSymbolsWithIntradayQuoteSubscriptions()
                        .ForEach(async symbol => await updateAndSendIntradayQuotes(symbol));

                mre.Set();
            }
            void finishUpdatingIntradayQuotes()
            {
                cts.Cancel();
                mre.Wait();
                httpClient.Dispose();
            }
        }

        public static event Action<string, string> ReceiveIntradayQuotesJson;
        public static event Action<Instrument, ISet<Quote>> ReceiveQuotes;

        private static void addQuotesSubscription(
            string tickerSymbol,
            string connectionId)
        {
            intradayQuoteSubscriptionsByTickerSymbol.TryAdd(tickerSymbol, new ConcurrentDictionary<string, byte>());
            intradayQuoteSubscriptionsByTickerSymbol[tickerSymbol].TryAdd(connectionId, default);
        }
        private static void removeIntradayQuoteSubscription(
            string tickerSymbol,
            string connectionId)
        {
            if (intradayQuoteSubscriptionsByTickerSymbol.TryGetValue(tickerSymbol, out IDictionary<string, byte> subscriptionsForTickerSymbol))
                subscriptionsForTickerSymbol.Remove(connectionId);
        }
        private static async Task removeAllIntradayQuoteSubscriptionsForConnection(
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

        private static async Task<string> getIntradayQuotesCsvAsync(
            string tickerSymbol)
        {
            using (HttpResponseMessage response = await httpClient.GetAsync(
                $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={tickerSymbol}&interval=1min&apikey={apiKey}&datatype=csv"))
            using (HttpContent content = response.Content)
                return await content.ReadAsStringAsync();
        }

        private static async Task updateAndSendIntradayQuotes(
            string symbol)
        {
            string csv = await getIntradayQuotesCsvAsync(symbol);

            ImmutableSortedSet<Quote> requestQuotes = new IntradayQuoteCsvTranslator()
                .ToQuotes(csv)
                .ToImmutableSortedSet();

            int i = requestQuotes.Count;

            if (quotesByInstrument.TryGetValue(symbol, out ImmutableSortedSet<Quote> quotes) &&
                !quotes.IsEmpty &&
                quotes[quotes.Count - 1].Time is DateTime lastUpdate)
            {
                SortedSet<Quote> newQuotes = new SortedSet<Quote>();

                while (requestQuotes[--i].Time > lastUpdate)
                    newQuotes.Add(requestQuotes[i]);

                if (newQuotes.Count > 0)
                {
                    ImmutableSortedSet<Quote>.Builder quoteAdder = quotes.ToBuilder();

                    foreach (Quote quote in newQuotes)
                        quoteAdder.Add(quote);

                    quotesByInstrument[symbol] = quoteAdder.ToImmutable();
                    ReceiveQuotes?.Invoke(symbol, newQuotes);
                }
            }
            else
            {
                quotesByInstrument[symbol] = requestQuotes;
                ReceiveQuotes?.Invoke(symbol, requestQuotes);
            }
        }
            
    }
}
