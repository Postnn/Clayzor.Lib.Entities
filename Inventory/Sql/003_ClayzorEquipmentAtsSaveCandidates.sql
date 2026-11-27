-- Inventory. Выполняется администратором после 002_ClayzorEquipmentAtsWorkflow.sql.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.ClayzorEquipmentAtsWorkflow', N'PlanPrepared') IS NULL
    BEGIN
        -- Операции явной настройки из версии 002 уже имеют подготовленный план.
        ALTER TABLE dbo.ClayzorEquipmentAtsWorkflow
            ADD PlanPrepared bit NOT NULL
                CONSTRAINT DF_ClayzorEquipmentAtsWorkflow_PlanPrepared DEFAULT (1) WITH VALUES;
    END;

    IF OBJECT_ID(N'dbo.ClayzorEquipmentAtsCandidate', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorEquipmentAtsCandidate
        (
            AttemptId uniqueidentifier NOT NULL,
            Kind tinyint NOT NULL,
            TargetId int NOT NULL,
            Deleting bit NOT NULL,
            CONSTRAINT PK_ClayzorEquipmentAtsCandidate PRIMARY KEY (AttemptId, Kind, TargetId),
            CONSTRAINT FK_ClayzorEquipmentAtsCandidate_Workflow FOREIGN KEY (AttemptId)
                REFERENCES dbo.ClayzorEquipmentAtsWorkflow (AttemptId),
            CONSTRAINT CK_ClayzorEquipmentAtsCandidate_Target CHECK
                (TargetId > 0 AND (Kind = 1 OR (Kind = 2 AND Deleting = 0)))
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
