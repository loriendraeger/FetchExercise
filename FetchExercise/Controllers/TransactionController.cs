using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FetchExercise.Models;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FetchExercise.Controllers
{
    [Route("api/[controller]")]
    public class TransactionController : ControllerBase
    {
        private TransactionContext _context;

        public TransactionController(TransactionContext context)
        {
            _context = context;
        }


        /// <summary>
        /// API endpoint for adding new transactions to the database.
        /// </summary>
        /// <param name="value">The transaction to add.</param>
        [HttpPost]
        public void Post([FromBody] TransactionInput value)
        {
            long? pointsRemaining = null;


            //this transaction has positive points, so see if there are any negative transactions that
            //came afterward (by timestamp) that weren't able to distribute their points
            if (value.Points > 0)
            {
                pointsRemaining = value.Points;

                IEnumerable<Transaction> txToMatch = FindTransactionsMissingMatch(value.Payer, value.Timestamp);
                IEnumerator<Transaction> txEnumerator = txToMatch.GetEnumerator();

                while (pointsRemaining > 0)
                {
                    if (!txEnumerator.MoveNext())
                    {
                        break;
                    }

                    //this transaction consumes all remaining points
                    if (pointsRemaining <= txEnumerator.Current.PointsRemaining * -1)
                    {
                        txEnumerator.Current.PointsRemaining += pointsRemaining;
                        pointsRemaining = 0;
                    }
                    //some points will be left over
                    else
                    {
                        pointsRemaining += txEnumerator.Current.PointsRemaining ?? 0;
                        txEnumerator.Current.PointsRemaining = null;
                    }
                }
            }


            if (value.Points < 0)
            {
                IEnumerable<Transaction> availableTx = FindSpendableTransactions(value.Payer, value.Timestamp);
                IEnumerator<Transaction> txEnumerator = availableTx.GetEnumerator();

                long pointsToDistribute = value.Points * -1;

                while (pointsToDistribute > 0)
                {                    
                    if (!txEnumerator.MoveNext())
                    {
                        break;
                    }

                    //this transaction can take all remaining points
                    if (pointsToDistribute <= txEnumerator.Current.PointsRemaining)
                    {
                        txEnumerator.Current.PointsRemaining -= pointsToDistribute;
                        pointsToDistribute = 0;
                    }
                    //some points will be left over
                    else
                    {
                        pointsToDistribute -= txEnumerator.Current.PointsRemaining ?? 0;
                        txEnumerator.Current.PointsRemaining = 0;
                    }
                }

                //we couldn't redistribute everything, so there is some outstanding negative balance that will need resolving later
                if (pointsToDistribute > 0)
                {
                    pointsRemaining = pointsToDistribute * -1;
                }
            }

            _context.Add(new Transaction()
            {
                Payer = value.Payer,
                Points = value.Points,
                Timestamp = value.Timestamp,
                PointsRemaining = pointsRemaining
            });

            _context.SaveChanges();
        }

        /// <summary>
        /// API to get all transactions (not specified in the requirements).
        /// </summary>
        /// <returns>All the transactions in the database.</returns>
        [HttpGet]
        public ActionResult<IEnumerable<Transaction>> GetAll()
        {
            return _context.Transactions.ToList<Transaction>();
        }

        /// <summary>
        /// API for getting the current balances by payer.
        /// </summary>
        /// <returns>A dictionary of payers and the current balances for those payers.</returns>
        //   /api/transaction/balances
        [HttpGet("balances")]
        public ActionResult<Dictionary<string, long>> GetBalances()
        {
            Dictionary<string, long> result = new Dictionary<string, long>();

            /*         
             * With a real database I would want to see the actual SQL emitted here.
             * If it wasn't something along the lines of this:
             * SELECT Payer,
             *        SUM( PointsRemaining ) SumPoints
             *   FROM Transactions
             *   WHERE PointsRemaining IS NOT NULL
             *   GROUP BY Payer
             *   
             * I would probably choose to write the SQL by hand.
             */

            var query = from tx in _context.Transactions
                        where tx.PointsRemaining != null
                        group tx by tx.Payer into payerGroups
                        select new { Payer = payerGroups.Key, SumPoints = payerGroups.Sum(tx => tx.PointsRemaining ?? 0) };

            foreach(var remainingPoints in query.AsEnumerable())
            {
                result.Add(remainingPoints.Payer, remainingPoints.SumPoints);
            }

            return result;
        }

        /// <summary>
        /// API for spending points.
        /// </summary>
        /// <param name="value">An object containing how many points to spend.</param>
        /// <returns>Resulting points spent by payer.</returns>
        //   /api/transaction/spend
        [HttpPost("spend")]
        public ActionResult<IEnumerable<TransactionOutput>> SpendPoints([FromBody] SpendInput value)
        {

            long pointsToDistribute = value.Points;
            Dictionary<string, long> pointsSpentByPayer = new Dictionary<string, long>();

            IEnumerable<Transaction> availableTx = FindSpendableTransactions();
            IEnumerator<Transaction> txEnumerator = availableTx.GetEnumerator();

            while (pointsToDistribute > 0)
            {
                if (!txEnumerator.MoveNext())
                {
                    break;
                }

                string payer = txEnumerator.Current.Payer;

                if (!pointsSpentByPayer.ContainsKey(payer))
                {
                    pointsSpentByPayer.Add(payer, 0);
                }

                //this transaction can take all remaining points
                if (pointsToDistribute <= txEnumerator.Current.PointsRemaining)
                {
                    pointsSpentByPayer[payer] = pointsSpentByPayer[payer] + pointsToDistribute;
                    txEnumerator.Current.PointsRemaining -= pointsToDistribute;
                    pointsToDistribute = 0;
                }
                //some points will be left over
                else
                {
                    pointsSpentByPayer[payer] = pointsSpentByPayer[payer] + txEnumerator.Current.PointsRemaining ?? 0;
                    pointsToDistribute -= txEnumerator.Current.PointsRemaining ?? 0;
                    txEnumerator.Current.PointsRemaining = 0;
                }
            }

            //we couldn't redistribute everything
            if (pointsToDistribute > 0)
            {
                return null;
            }

            DateTime now = DateTime.Now;  //use the same timestamp for all resulting transactions

            List<TransactionOutput> results = new List<TransactionOutput>();

            //we could redistribute everything, so make the transactions to save along with the result set
            foreach(KeyValuePair<string, long> kv in pointsSpentByPayer)
            {
                string payer = kv.Key;
                long points = kv.Value * -1;

                results.Add(new TransactionOutput() { Payer = payer, Points = points });
                _context.Add(new Transaction()
                {
                    Payer = payer,
                    Points = points,
                    Timestamp = now,
                    PointsRemaining = null
                });
            }

            _context.SaveChanges();
            return results;

        }

        /// <summary>
        /// Finds transactions that have a positive PointsRemaining (can be spent)
        /// and orders them by timestamp (the order they should be spent).
        /// </summary>
        /// <param name="payer">Limit the transactions to this payer. Null means do not filter by payer.</param>
        /// <param name="onOrBeforeTimestamp">Limit transactions to having a timestamp on or before this DateTime. Null means do not filter by timestamp.</param>
        /// <returns>Transaction objects that can be spent in the order to spend them.</returns>
        private IEnumerable<Transaction> FindSpendableTransactions(string payer = null, DateTime? onOrBeforeTimestamp = null)
        {
            var query = from tx in _context.Transactions
                        where tx.PointsRemaining > 0
                        && (payer != null ? tx.Payer == payer : true)
                        && (onOrBeforeTimestamp != null ? tx.Timestamp <= onOrBeforeTimestamp : true)
                        orderby tx.Timestamp
                        select tx;

            return query.AsEnumerable();
        }

        /// <summary>
        /// Finds negative transactions that have outstanding point spend that needs to be matched to positive transactions.
        /// </summary>
        /// <param name="payer">Limit the transactions to this payer.</param>
        /// <param name="onOrAfterTimestamp">Limit transactions returned to having this timestamp or a later one.</param>
        /// <returns>A list of positive transactions that could be matched, ordered by Timestamp.</returns>
        private IEnumerable<Transaction> FindTransactionsMissingMatch(string payer, DateTime onOrAfterTimestamp)
        {
            var query = from tx in _context.Transactions
                        where tx.PointsRemaining < 0
                        && tx.Payer == payer
                        && tx.Timestamp >= onOrAfterTimestamp
                        orderby tx.Timestamp
                        select tx;

            return query.AsEnumerable();
        }
    }

    public class TransactionInput
    {
        public string Payer { get; set; }
        public long Points { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TransactionOutput
    {
        public string Payer { get; set; }
        public long Points { get; set; }
    }

    public class SpendInput
    {
        public long Points { get; set; }
    }

}
