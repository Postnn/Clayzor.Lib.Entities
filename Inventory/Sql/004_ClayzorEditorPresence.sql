-- Inventory. Аренды создаёт серверный путь после подтверждения сотрудника и доступа к сущности.
-- Администратор выдаёт доверенному исполнителю только EXECUTE на две процедуры.
-- Прямой доступ к таблице и EXECUTE для произвольных пользователей SQL не выдаются.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ClayzorEditorPresence', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ClayzorEditorPresence
        (
            LeaseId uniqueidentifier NOT NULL,
            EntityHash binary(32) NOT NULL,
            EmployeeId int NOT NULL,
            DisplayName nvarchar(256) NOT NULL,
            Activity tinyint NOT NULL,
            HeartbeatUtc datetime2(7) NOT NULL,
            ExpiresUtc datetime2(7) NOT NULL,
            CONSTRAINT PK_ClayzorEditorPresence PRIMARY KEY (LeaseId),
            CONSTRAINT CK_ClayzorEditorPresence_Employee CHECK (EmployeeId > 0),
            CONSTRAINT CK_ClayzorEditorPresence_Activity CHECK (Activity IN (0, 1)),
            CONSTRAINT CK_ClayzorEditorPresence_Expiry CHECK (ExpiresUtc > HeartbeatUtc)
        );

        CREATE NONCLUSTERED INDEX IX_ClayzorEditorPresence_EntityExpiry
            ON dbo.ClayzorEditorPresence (EntityHash, ExpiresUtc)
            INCLUDE (EmployeeId, DisplayName, Activity, HeartbeatUtc);
        CREATE NONCLUSTERED INDEX IX_ClayzorEditorPresence_Expiry
            ON dbo.ClayzorEditorPresence (ExpiresUtc);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @SetupError nvarchar(2048);
    SET @SetupError = ERROR_MESSAGE();
    RAISERROR(N'%s', 16, 1, @SetupError);
END CATCH;
GO

IF OBJECT_ID(N'dbo.ClayzorPresenceHeartbeat', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.ClayzorPresenceHeartbeat AS RETURN 0;');
GO
ALTER PROCEDURE dbo.ClayzorPresenceHeartbeat
    @LeaseId uniqueidentifier,
    @Renew bit,
    @EntityHash binary(32),
    @EmployeeId int,
    @DisplayName nvarchar(256),
    @Activity tinyint,
    @LifetimeMilliseconds int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @LifetimeMilliseconds IS NULL OR @LifetimeMilliseconds <= 0 OR @LeaseId IS NULL
       OR @EmployeeId IS NULL OR @EmployeeId <= 0 OR @Activity IS NULL OR @Activity NOT IN (0, 1)
       OR LEN(ISNULL(@DisplayName, N'')) = 0 OR @EntityHash IS NULL OR @Renew IS NULL
    BEGIN
        RAISERROR(N'Некорректная аренда присутствия.', 16, 1);
        RETURN;
    END;

    DECLARE @Now datetime2(7);
    SET @Now = SYSUTCDATETIME();
    DECLARE @Expires datetime2(7);
    SET @Expires = DATEADD(millisecond, @LifetimeMilliseconds, @Now);

    IF @Renew = 1
    BEGIN
        UPDATE dbo.ClayzorEditorPresence
           SET DisplayName = @DisplayName, Activity = @Activity, HeartbeatUtc = @Now, ExpiresUtc = @Expires
         WHERE LeaseId = @LeaseId AND EntityHash = @EntityHash AND EmployeeId = @EmployeeId
           AND ExpiresUtc > SYSUTCDATETIME();
        IF @@ROWCOUNT = 0
        BEGIN
            SELECT EmployeeId, DisplayName, Activity FROM dbo.ClayzorEditorPresence WHERE 1 = 0;
            RETURN;
        END;
    END
    ELSE
        INSERT INTO dbo.ClayzorEditorPresence (LeaseId, EntityHash, EmployeeId, DisplayName, Activity, HeartbeatUtc, ExpiresUtc)
        VALUES (@LeaseId, @EntityHash, @EmployeeId, @DisplayName, @Activity, @Now, @Expires);

    DELETE TOP (128) FROM dbo.ClayzorEditorPresence WHERE ExpiresUtc <= @Now;

    -- Строка вызывающего — подтверждение аренды. Остальные строки группируются по сотруднику.
    ;WITH ActiveEditors AS
    (
        SELECT EmployeeId, DisplayName, Activity,
               ROW_NUMBER() OVER(PARTITION BY EmployeeId
                   ORDER BY Activity DESC, HeartbeatUtc DESC, LeaseId) AS RowNumber
          FROM dbo.ClayzorEditorPresence
         WHERE EntityHash = @EntityHash AND EmployeeId <> @EmployeeId
           AND ExpiresUtc > SYSUTCDATETIME()
    )
    SELECT @EmployeeId AS EmployeeId, @DisplayName AS DisplayName, @Activity AS Activity
    UNION ALL
    SELECT EmployeeId, DisplayName, Activity FROM ActiveEditors WHERE RowNumber = 1;
END;
GO

IF OBJECT_ID(N'dbo.ClayzorPresenceLeave', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.ClayzorPresenceLeave AS RETURN 0;');
GO
ALTER PROCEDURE dbo.ClayzorPresenceLeave
    @LeaseId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.ClayzorEditorPresence WHERE LeaseId = @LeaseId;
END;
GO
