using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using stocks.Models.Translators.Abstractions;

namespace stocks.Models.Translators.AlphaVantage
{
    public class IntradayQuoteCsvTranslator :
        IQuoteTranslator<string>
    {
        public IEnumerable<Quote> ToQuotes(
            string quotesInOtherFormat)
        {
            // csv format:
            // timestamp,open,high,low,close,volume
            // first row contains column headers

            ISet<Quote> quotes = new HashSet<Quote>();
            ReadOnlySpan<char> text = quotesInOtherFormat;

            _ = shiftLine(ref text);

            while (shiftLine(ref text) is var line && line.IndexOf(',') != -1)
            {
                ReadOnlySpan<char> timestamp = shift(ref line);
                _ = shift(ref line);
                _ = shift(ref line);
                _ = shift(ref line);
                ReadOnlySpan<char> close = shift(ref line);

                quotes.Add(new Quote(
                    time: DateTime.Parse(timestamp),
                    price: decimal.Parse(close, NumberStyles.Number, CultureInfo.InvariantCulture)));
            }

            return quotes;

            ReadOnlySpan<char> shiftLine(
                ref ReadOnlySpan<char> csv)
            {
                int nextLfIndex = csv.IndexOf('\n');
                ReadOnlySpan<char> line = csv.Slice(0, nextLfIndex + 1);
                csv = csv.Slice(nextLfIndex + 1);
                return line;
            }

            ReadOnlySpan<char> shift(
                ref ReadOnlySpan<char> csv)
            {
                if (csv.IndexOf(',') is var nextCommaIndex && nextCommaIndex != -1)
                {
                    ReadOnlySpan<char> nextValue = csv.Slice(0, nextCommaIndex);
                    csv = csv.Slice(nextCommaIndex + 1);
                    return nextValue;
                }
                else if (csv.IndexOf('\r') is var nextCrIndex && nextCrIndex != -1)
                {
                    ReadOnlySpan<char> nextValue = csv.Slice(0, nextCrIndex);
                    csv = ReadOnlySpan<char>.Empty;
                    return nextValue;
                }
                else
                {
                    ReadOnlySpan<char> nextValue = csv.Slice(0);
                    csv = ReadOnlySpan<char>.Empty;
                    return nextValue;
                }
                
            } 
        }
    }
}
