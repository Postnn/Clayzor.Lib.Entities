-- Inventory. Скрипт выполняется администратором до включения журнала АТС оборудования.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ClayzorEquipmentAtsWorkflow', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorEquipmentAtsWorkflow
        (
            AttemptId uniqueidentifier NOT NULL,
            EmployeeId int NOT NULL,
            EquipmentId int NOT NULL,
            Origin tinyint NOT NULL,
            ExpectedChangedAtUtc datetime2(7) NOT NULL,
            OwnerId int NULL,
            ContextFingerprint char(64) NOT NULL,
            State tinyint NOT NULL,
            CreatedUtc datetime2(7) NOT NULL,
            ClosedUtc datetime2(7) NULL,
            CONSTRAINT PK_ClayzorEquipmentAtsWorkflow PRIMARY KEY (AttemptId),
            CONSTRAINT CK_ClayzorEquipmentAtsWorkflow_Inputs CHECK
                (AttemptId <> '00000000-0000-0000-0000-000000000000'
                 AND EmployeeId > 0 AND EquipmentId > 0 AND Origin IN (0, 1)),
            CONSTRAINT CK_ClayzorEquipmentAtsWorkflow_Fingerprint CHECK
                (LEN(ContextFingerprint) = 64
                 AND ContextFingerprint COLLATE Latin1_General_BIN2 NOT LIKE '%[^0-9a-f]%'),
            CONSTRAINT CK_ClayzorEquipmentAtsWorkflow_State CHECK
                ((State = 0 AND ClosedUtc IS NULL) OR (State IN (1, 2) AND ClosedUtc IS NOT NULL))
        );

        CREATE NONCLUSTERED INDEX IX_ClayzorEquipmentAtsWorkflow_EquipmentEmployee
            ON dbo.ClayzorEquipmentAtsWorkflow (EquipmentId, EmployeeId, State, CreatedUtc DESC);
    END;

    IF OBJECT_ID(N'dbo.ClayzorEquipmentAtsStep', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorEquipmentAtsStep
        (
            AttemptId uniqueidentifier NOT NULL,
            StepIndex int NOT NULL,
            Kind tinyint NOT NULL,
            TargetId int NOT NULL,
            Deleting bit NOT NULL,
            State tinyint NOT NULL,
            CONSTRAINT PK_ClayzorEquipmentAtsStep PRIMARY KEY (AttemptId, StepIndex),
            CONSTRAINT FK_ClayzorEquipmentAtsStep_Workflow FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorEquipmentAtsWorkflow (AttemptId),
            CONSTRAINT UQ_ClayzorEquipmentAtsStep_Target UNIQUE (AttemptId, Kind, TargetId, Deleting),
            CONSTRAINT CK_ClayzorEquipmentAtsStep_Inputs CHECK
                (StepIndex >= 0 AND TargetId > 0 AND (Kind = 1 OR (Kind = 2 AND Deleting = 0))),
            CONSTRAINT CK_ClayzorEquipmentAtsStep_State CHECK (State IN (0, 1, 2))
        );
    END;

    IF OBJECT_ID(N'dbo.ClayzorEquipmentAtsScope', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorEquipmentAtsScope
        (
            AttemptId uniqueidentifier NOT NULL,
            ConflictKey nvarchar(256) NOT NULL,
            OpenSlot uniqueidentifier NOT NULL,
            CONSTRAINT PK_ClayzorEquipmentAtsScope PRIMARY KEY (AttemptId, ConflictKey),
            CONSTRAINT FK_ClayzorEquipmentAtsScope_Workflow FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorEquipmentAtsWorkflow (AttemptId),
            CONSTRAINT CK_ClayzorEquipmentAtsScope_ConflictKey CHECK (LEN(ConflictKey) > 0),
            CONSTRAINT CK_ClayzorEquipmentAtsScope_OpenSlot CHECK
                (OpenSlot = '00000000-0000-0000-0000-000000000000' OR OpenSlot = AttemptId)
        );

        CREATE UNIQUE NONCLUSTERED INDEX UX_ClayzorEquipmentAtsScope_OpenConflict
            ON dbo.ClayzorEquipmentAtsScope (ConflictKey, OpenSlot);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @message nvarchar(2048);
    SELECT @message = ERROR_MESSAGE();
    RAISERROR(N'%s', 16, 1, @message);
END CATCH;
