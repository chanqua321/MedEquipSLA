# Maintenance migrations

EF Core Code First migrations belong only to MedEquip_Maintenance. InitialCreate implements approved ERD v0.1; review the generated SQL script in docs/database/migrations. Never use EnsureCreated or runtime automatic migration. Apply with a scoped migration identity.

Apply via the repository scripts documented in [database setup](../../../../../../docs/database/SETUP.md). SQL scripts are generated review artifacts; C# migrations are the source of schema changes.
