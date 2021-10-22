# FetchExercise

Demo of web APIs using ASP.NET on .NET Core 3.

## Running

Clone the repository and then open and run FetchExercise.sln. There are two NuGet package dependencies from Microsoft.

-  Microsoft.EntityFrameworkCore
-  Microsoft.EntityFrameworkCore.InMemory

By default the applications runs on at https://localhost:5001/

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
