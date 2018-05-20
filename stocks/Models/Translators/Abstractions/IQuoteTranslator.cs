using System.Collections.Generic;

namespace stocks.Models.Translators.Abstractions
{
    public interface IQuoteTranslator<in T>
    {
        IEnumerable<Quote> ToQuotes(
            T quotesInOtherFormat);
    }
}
