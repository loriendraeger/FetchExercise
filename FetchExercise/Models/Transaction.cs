using System;
namespace FetchExercise.Models
{
    /*
     * In this data model, everything is stored as a Transaction.
     * Each transaction also represents a batch of points that can be consumed (stored in PointsRemaining).
     * 
     * Positive transactions generally start with PointsRemaining equal to Points and then PointsRemaining
     * is consumed by negative transactions. Negative transactions will typically have a null PointsRemaining.
     * 
     * However, if we receive a negative transaction and there are corresponding positive transactions for
     * it to consume, we mark note that it is outstanding by setting a negative PointsRemaining.
     * 
     * All of this logic is handled in the TransactionController Post method.
     */

    public class Transaction
    {
        public long Id { get; set; }
        public string Payer { get; set; }
        public long Points { get; set; }
        public DateTime Timestamp { get; set; }
        public long? PointsRemaining { get; set; }

        public Transaction()
        {
        }
    }
}
