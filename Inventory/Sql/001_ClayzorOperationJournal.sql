-- Inventory. Выполняется администратором в базе приложения перед включением журнала.
-- Права на таблицу получает только согласованный серверный write-path.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ClayzorOperationJournal', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorOperationJournal
        (
            AttemptId uniqueidentifier NOT NULL,
            EmployeeId int NOT NULL,
            ConflictKey nvarchar(256) NOT NULL,
            CommandKind nvarchar(64) NOT NULL,
            State tinyint NOT NULL,
            CreatedUtc datetime2(7) NOT NULL,
            UpdatedUtc datetime2(7) NOT NULL,
            SettledUtc datetime2(7) NULL,
            ResultEntityId int NULL,
            OpenSlot uniqueidentifier NOT NULL,
            CONSTRAINT PK_ClayzorOperationJournal PRIMARY KEY (AttemptId),
            CONSTRAINT CK_ClayzorOperationJournal_Attempt CHECK (AttemptId <> '00000000-0000-0000-0000-000000000000'),
            CONSTRAINT CK_ClayzorOperationJournal_Employee CHECK (EmployeeId > 0),
            CONSTRAINT CK_ClayzorOperationJournal_State CHECK
            (
                (State IN (0, 1, 2) AND SettledUtc IS NULL AND ResultEntityId IS NULL
                    AND OpenSlot = '00000000-0000-0000-0000-000000000000')
                OR (State = 3 AND SettledUtc IS NOT NULL AND OpenSlot = AttemptId)
                OR (State = 4 AND SettledUtc IS NOT NULL AND ResultEntityId IS NULL AND OpenSlot = AttemptId)
            )
        );

        CREATE UNIQUE NONCLUSTERED INDEX UX_ClayzorOperationJournal_OpenConflict
            ON dbo.ClayzorOperationJournal (ConflictKey, OpenSlot);

        CREATE NONCLUSTERED INDEX IX_ClayzorOperationJournal_EmployeeConflict
            ON dbo.ClayzorOperationJournal (EmployeeId, ConflictKey, CreatedUtc DESC);
    END;

    IF OBJECT_ID(N'dbo.ClayzorOperationScope', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorOperationScope
        (
            AttemptId uniqueidentifier NOT NULL,
            ConflictKey nvarchar(256) NOT NULL,
            OpenSlot uniqueidentifier NOT NULL,
            CONSTRAINT PK_ClayzorOperationScope PRIMARY KEY (AttemptId, ConflictKey),
            CONSTRAINT FK_ClayzorOperationScope_Attempt FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorOperationJournal (AttemptId),
            CONSTRAINT CK_ClayzorOperationScope_OpenSlot CHECK
                (OpenSlot = '00000000-0000-0000-0000-000000000000' OR OpenSlot = AttemptId)
        );

        CREATE UNIQUE NONCLUSTERED INDEX UX_ClayzorOperationScope_OpenConflict
            ON dbo.ClayzorOperationScope (ConflictKey, OpenSlot);
    END;

    IF OBJECT_ID(N'dbo.ClayzorLegacyOperationEvidence', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorLegacyOperationEvidence
        (
            AttemptId uniqueidentifier NOT NULL,
            EmployeeId int NOT NULL,
            ConflictKey nvarchar(256) NOT NULL,
            CommandKind nvarchar(64) NOT NULL,
            ImportedUtc datetime2(7) NOT NULL,
            ResolvedUtc datetime2(7) NULL,
            CONSTRAINT PK_ClayzorLegacyOperationEvidence PRIMARY KEY (AttemptId),
            CONSTRAINT CK_ClayzorLegacyOperationEvidence_Employee CHECK (EmployeeId > 0)
        );

        CREATE NONCLUSTERED INDEX IX_ClayzorLegacyOperationEvidence_Conflict
            ON dbo.ClayzorLegacyOperationEvidence (ConflictKey, ResolvedUtc);
    END;

    IF OBJECT_ID(N'dbo.ClayzorLegacyOperationScope', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorLegacyOperationScope
        (
            AttemptId uniqueidentifier NOT NULL,
            ConflictKey nvarchar(256) NOT NULL,
            CONSTRAINT PK_ClayzorLegacyOperationScope PRIMARY KEY (AttemptId, ConflictKey),
            CONSTRAINT FK_ClayzorLegacyOperationScope_Evidence FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorLegacyOperationEvidence (AttemptId)
        );
        CREATE NONCLUSTERED INDEX IX_ClayzorLegacyOperationScope_Conflict
            ON dbo.ClayzorLegacyOperationScope (ConflictKey, AttemptId);
    END;

    IF OBJECT_ID(N'dbo.ClayzorTelWorkflow', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorTelWorkflow
        (
            AttemptId uniqueidentifier NOT NULL,
            EmployeeId int NOT NULL,
            OriginalPhoneId int NOT NULL,
            Deleting bit NOT NULL,
            ConflictKey nvarchar(256) NOT NULL,
            ResultPhoneId int NULL,
            ResultConflictKey nvarchar(256) NULL,
            PreStepCount int NOT NULL,
            PostStepCount int NULL,
            SqlConfirmed bit NOT NULL,
            CreatedUtc datetime2(7) NOT NULL,
            ClosedUtc datetime2(7) NULL,
            OpenSlot uniqueidentifier NOT NULL,
            CONSTRAINT PK_ClayzorTelWorkflow PRIMARY KEY (AttemptId),
            CONSTRAINT CK_ClayzorTelWorkflow_Inputs CHECK
                (EmployeeId > 0 AND OriginalPhoneId >= 0 AND PreStepCount >= 0
                 AND (PostStepCount IS NULL OR PostStepCount >= 0)
                 AND (OriginalPhoneId > 0 OR (Deleting = 0 AND PreStepCount = 0))),
            CONSTRAINT CK_ClayzorTelWorkflow_Sql CHECK
                ((SqlConfirmed = 0 AND ResultPhoneId IS NULL AND ResultConflictKey IS NULL)
                 OR (SqlConfirmed = 1 AND ResultPhoneId IS NOT NULL AND ResultPhoneId > 0
                      AND ResultConflictKey IS NOT NULL AND PostStepCount IS NOT NULL)),
            CONSTRAINT CK_ClayzorTelWorkflow_OpenSlot CHECK
                ((ClosedUtc IS NULL AND OpenSlot = '00000000-0000-0000-0000-000000000000')
                 OR (ClosedUtc IS NOT NULL AND OpenSlot = AttemptId))
        );
        CREATE UNIQUE NONCLUSTERED INDEX UX_ClayzorTelWorkflow_OpenConflict
            ON dbo.ClayzorTelWorkflow (ConflictKey, OpenSlot);
        CREATE NONCLUSTERED INDEX IX_ClayzorTelWorkflow_ResultConflict
            ON dbo.ClayzorTelWorkflow (ResultConflictKey, OpenSlot);
    END;

    IF OBJECT_ID(N'dbo.ClayzorTelWorkflowStep', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorTelWorkflowStep
        (
            AttemptId uniqueidentifier NOT NULL,
            BeforeSql bit NOT NULL,
            StepIndex int NOT NULL,
            Fingerprint char(64) NOT NULL,
            State tinyint NOT NULL,
            CONSTRAINT PK_ClayzorTelWorkflowStep PRIMARY KEY (AttemptId, BeforeSql, StepIndex),
            CONSTRAINT FK_ClayzorTelWorkflowStep_Workflow FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorTelWorkflow (AttemptId),
            CONSTRAINT CK_ClayzorTelWorkflowStep_State CHECK (StepIndex >= 0 AND State IN (0, 1, 2))
        );
    END;

    IF OBJECT_ID(N'dbo.ClayzorLocationFactReceipt', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorLocationFactReceipt
        (
            AttemptId uniqueidentifier NOT NULL,
            EquipmentId int NOT NULL,
            LocationId int NOT NULL,
            CheckedAtUtc datetime NOT NULL,
            CONSTRAINT PK_ClayzorLocationFactReceipt PRIMARY KEY (AttemptId),
            CONSTRAINT FK_ClayzorLocationFactReceipt_Attempt FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorOperationJournal (AttemptId)
        );
        CREATE NONCLUSTERED INDEX IX_ClayzorLocationFactReceipt_Scope
            ON dbo.ClayzorLocationFactReceipt (EquipmentId, LocationId, CheckedAtUtc DESC);
    END;

    IF OBJECT_ID(N'dbo.ClayzorOperationTextReceipt', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorOperationTextReceipt
        (
            AttemptId uniqueidentifier NOT NULL,
            ResultKey nvarchar(128) NOT NULL,
            CONSTRAINT PK_ClayzorOperationTextReceipt PRIMARY KEY (AttemptId),
            CONSTRAINT FK_ClayzorOperationTextReceipt_Attempt FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorOperationJournal (AttemptId)
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @message nvarchar(2048);
    SELECT @message = ERROR_MESSAGE();
    RAISERROR(N'%s', 16, 1, @message);
END CATCH;
