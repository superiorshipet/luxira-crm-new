# Local isolated infrastructure

This environment is exclusively for development and automated integration tests. Never paste a production connection string or restore an unapproved production backup into it.

1. Copy `.env.example` to `.env` and replace both local-only passwords.
2. Start `compose.local.yml` when SQL/Redis integration work is explicitly being tested.
3. Create or restore only an approved anonymized schema/data set into `LuxiraLocal`.
4. Apply reviewed development migrations/scripts manually; the API never migrates at startup.
5. Keep ports `14339` and `16379` local and out of production configuration.

The initial `LuxiraReadDbContext` is query-only and maps only the columns required by the first Delivery read slice. It does not own a migration chain and must not be used to infer or modify the production schema.
