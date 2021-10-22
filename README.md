# FetchExercise

Demo of web APIs using ASP.NET on .NET Core 3.

## Running

Clone the repository and then open and run FetchExercise.sln. There are two NuGet package dependencies from Microsoft.

-  Microsoft.EntityFrameworkCore
-  Microsoft.EntityFrameworkCore.InMemory

By default the application runs at https://localhost:5001/

## API Endpoints

### Add a transaction

POST to /api/transaction

### See point balances

GET /api/transaction/balances

### Spend points

POST to /api/transaction/spend

### Get all transactions

This is useful for debugging.

GET /api/transaction

## Implementation Notes

For this exercise I used the Microsoft's in memory fake database over a real RDBMS platform. With a real database, I would add a partial index to the Timestamp column for transactions where PointsRemaining <> 0 and PointsRemaining is not null. This would allow fast searching of matching transactions.

Also this implementation favors avoiding duplicate data over speed. As a result, even if this used a real database with the above index, the see point balances API call would be quite expensive because every transaction needs to be checked since payers with zero balances need to be returned.
