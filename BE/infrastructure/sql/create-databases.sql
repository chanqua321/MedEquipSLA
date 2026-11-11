-- Infrastructure bootstrap only. Business schema must come from service-owned EF migrations.
IF DB_ID(N'MedEquip_Identity') IS NULL CREATE DATABASE [MedEquip_Identity];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'medequip_identity_app')
    CREATE LOGIN [medequip_identity_app] WITH PASSWORD = '$(IDENTITY_DB_PASSWORD)', CHECK_POLICY=ON;
GO
USE [MedEquip_Identity];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'medequip_identity_app')
    CREATE USER [medequip_identity_app] FOR LOGIN [medequip_identity_app];
ALTER ROLE db_datareader ADD MEMBER [medequip_identity_app];
ALTER ROLE db_datawriter ADD MEMBER [medequip_identity_app];
GO
USE [master];
GO
IF DB_ID(N'MedEquip_Equipment') IS NULL CREATE DATABASE [MedEquip_Equipment];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'medequip_equipment_app')
    CREATE LOGIN [medequip_equipment_app] WITH PASSWORD = '$(EQUIPMENT_DB_PASSWORD)', CHECK_POLICY=ON;
GO
USE [MedEquip_Equipment];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'medequip_equipment_app')
    CREATE USER [medequip_equipment_app] FOR LOGIN [medequip_equipment_app];
ALTER ROLE db_datareader ADD MEMBER [medequip_equipment_app];
ALTER ROLE db_datawriter ADD MEMBER [medequip_equipment_app];
GO
USE [master];
GO
IF DB_ID(N'MedEquip_Maintenance') IS NULL CREATE DATABASE [MedEquip_Maintenance];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'medequip_maintenance_app')
    CREATE LOGIN [medequip_maintenance_app] WITH PASSWORD = '$(MAINTENANCE_DB_PASSWORD)', CHECK_POLICY=ON;
GO
USE [MedEquip_Maintenance];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'medequip_maintenance_app')
    CREATE USER [medequip_maintenance_app] FOR LOGIN [medequip_maintenance_app];
ALTER ROLE db_datareader ADD MEMBER [medequip_maintenance_app];
ALTER ROLE db_datawriter ADD MEMBER [medequip_maintenance_app];
GO
USE [master];
GO
IF DB_ID(N'MedEquip_Incident') IS NULL CREATE DATABASE [MedEquip_Incident];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'medequip_incident_app')
    CREATE LOGIN [medequip_incident_app] WITH PASSWORD = '$(INCIDENT_DB_PASSWORD)', CHECK_POLICY=ON;
GO
USE [MedEquip_Incident];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'medequip_incident_app')
    CREATE USER [medequip_incident_app] FOR LOGIN [medequip_incident_app];
ALTER ROLE db_datareader ADD MEMBER [medequip_incident_app];
ALTER ROLE db_datawriter ADD MEMBER [medequip_incident_app];
GO
USE [master];
GO
IF DB_ID(N'MedEquip_Notification') IS NULL CREATE DATABASE [MedEquip_Notification];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'medequip_notification_app')
    CREATE LOGIN [medequip_notification_app] WITH PASSWORD = '$(NOTIFICATION_DB_PASSWORD)', CHECK_POLICY=ON;
GO
USE [MedEquip_Notification];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'medequip_notification_app')
    CREATE USER [medequip_notification_app] FOR LOGIN [medequip_notification_app];
ALTER ROLE db_datareader ADD MEMBER [medequip_notification_app];
ALTER ROLE db_datawriter ADD MEMBER [medequip_notification_app];
GO
USE [master];
GO
