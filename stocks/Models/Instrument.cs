using System;

namespace stocks.Models
{
    public readonly struct Instrument :
        IEquatable<Instrument>,
        IEquatable<string>
    {
        public Instrument(
            string symbol)
            => Symbol = symbol.Trim().ToUpperInvariant();

        public string Symbol { get; }

        public bool Equals(
            Instrument other)
            => other.Symbol.Equals(Symbol);
        public bool Equals(
            string other)
            => Equals(new Instrument(other));

        public override int GetHashCode() 
            => Symbol.GetHashCode();

        public static implicit operator string(
            Instrument instrument)
            => instrument.Symbol;
        public static implicit operator Instrument(
            string symbol)
            => new Instrument(symbol);
    }
}
