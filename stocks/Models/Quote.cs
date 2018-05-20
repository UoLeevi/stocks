using System;

namespace stocks.Models
{
    public class Quote :
        IEquatable<Quote>,
        IComparable<Quote>
    {
        public Quote(
            DateTime time,
            decimal price)
        {
            Time = time;
            Price = price;
        }

        public DateTime Time { get; }
        public decimal Price { get; }

        public int CompareTo(
            Quote other) => Time.CompareTo(other.Time);

        public bool Equals(
            Quote other)
            => other is Quote quote
            && quote.Time.Equals(Time)
            && quote.Price.Equals(Price);

        public override bool Equals(
            object obj)
            => Equals(obj as Quote);

        public override int GetHashCode()
            => Time.GetHashCode();
    }
}
