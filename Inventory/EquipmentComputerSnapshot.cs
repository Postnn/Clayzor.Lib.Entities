using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Дневной снимок конфигурации компьютера.</summary>
public sealed class EquipmentComputerSnapshot
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Дата снимка.</summary>
    [Column(EquipmentComputerColumns.С)]
    public DateTime Date { get; set; }

    /// <summary>Общий объём RAM в Мб.</summary>
    [Column(EquipmentComputerColumns.RAM)]
    public int RamMb { get; set; }

    /// <summary>Сериализованные компоненты RAM.</summary>
    [Column(EquipmentComputerColumns.RAMDetail)]
    public string RamDetail { get; set; } = string.Empty;

    /// <summary>Общий объём HD в Гб.</summary>
    [Column(EquipmentComputerColumns.HD)]
    public float HdGb { get; set; }

    /// <summary>Сериализованные компоненты HD.</summary>
    [Column(EquipmentComputerColumns.HDDetail)]
    public string HdDetail { get; set; } = string.Empty;

    /// <summary>Общий объём SSD в Гб.</summary>
    [Column(EquipmentComputerColumns.SSD)]
    public int SsdGb { get; set; }

    /// <summary>Сериализованные компоненты SSD.</summary>
    [Column(EquipmentComputerColumns.SSDDetail)]
    public string SsdDetail { get; set; } = string.Empty;

    /// <summary>Первая стандартная конфигурация.</summary>
    [Column(EquipmentComputerColumns.КодСтандартнойКонфигурации1)]
    public int? Configuration1Id { get; set; }

    /// <summary>Дата первой стандартной конфигурации.</summary>
    [Column(EquipmentComputerColumns.ДатаСтандартнойКонфигурации1)]
    public DateTime? Configuration1Date { get; set; }

    /// <summary>Вторая стандартная конфигурация.</summary>
    [Column(EquipmentComputerColumns.КодСтандартнойКонфигурации2)]
    public int? Configuration2Id { get; set; }

    /// <summary>Дата второй стандартной конфигурации.</summary>
    [Column(EquipmentComputerColumns.ДатаСтандартнойКонфигурации2)]
    public DateTime? Configuration2Date { get; set; }

    /// <summary>Лицензия.</summary>
    [Column(EquipmentComputerColumns.Лицензия)]
    public string License { get; set; } = string.Empty;

    /// <summary>Примечания.</summary>
    [Column(EquipmentColumns.Примечания)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }

    /// <summary>Признак существования строки на момент открытия редактора.</summary>
    [NotMapped]
    public bool ExpectedExisting { get; set; }

    /// <summary>Название первой стандартной конфигурации.</summary>
    [Column(EquipmentComputerColumns.Конфигурация1)]
    public string Configuration1Name { get; set; } = string.Empty;

    /// <summary>Название второй стандартной конфигурации.</summary>
    [Column(EquipmentComputerColumns.Конфигурация2)]
    public string Configuration2Name { get; set; } = string.Empty;

    /// <summary>Компоненты RAM в Мб.</summary>
    [NotMapped]
    public IReadOnlyList<decimal> RamComponents { get; set; } = [];

    /// <summary>Компоненты HD в Гб.</summary>
    [NotMapped]
    public IReadOnlyList<decimal> HdComponents { get; set; } = [];

    /// <summary>Компоненты SSD в Гб.</summary>
    [NotMapped]
    public IReadOnlyList<decimal> SsdComponents { get; set; } = [];

    /// <summary>Разбирает сериализованные компоненты после чтения из БД.</summary>
    public void ParseDetails()
    {
        RamComponents = EquipmentComputerDetail.Parse(RamDetail);
        HdComponents = EquipmentComputerDetail.Parse(HdDetail);
        SsdComponents = EquipmentComputerDetail.Parse(SsdDetail);
    }

    /// <summary>
    /// Проверяет пары стандартных конфигураций и дат, актуальность дат на переданный день
    /// и соответствие числа RAM-компонентов фактическому числу банков модели.
    /// </summary>
    /// <param name="today">Текущий календарный день, определённый вызывающим контекстом.</param>
    /// <param name="memoryBanks">Число банков RAM фактической модели; null отключает эту проверку.</param>
    /// <returns>Текст первой ошибки либо null для допустимой конфигурации.</returns>
    public string? ValidateConfiguration(DateTime today, int? memoryBanks = null)
    {
        if (Configuration1Id is not null && Configuration1Date is null)
            return "Необходимо указать дату Стандартной конфигурации 1!";
        if (Configuration2Id is not null && Configuration2Date is null)
            return "Необходимо указать дату Стандартной конфигурации 2!";
        if (Configuration1Id is null && (Configuration1Date is not null || Configuration2Id is not null))
            return "Необходимо указать Стандартную конфигурацию 1!";
        if (Configuration2Id is null && Configuration2Date is not null)
            return "Необходимо указать Стандартную конфигурацию 2!";
        if (Configuration1Date?.Date > today.Date)
            return "Дата Стандартной конфигурации 1 не может быть больше текущей!";
        if (Configuration2Date?.Date > today.Date)
            return "Дата Стандартной конфигурации 2 не может быть больше текущей!";
        if (memoryBanks is not null && RamComponents.Count > memoryBanks.Value)
            return $"Количество компонентов RAM ({RamComponents.Count}) превышает число банков памяти модели ({memoryBanks.Value}).";
        return null;
    }

    /// <summary>Сохраняет дневной снимок через подтверждённый UPSERT.</summary>
    public Task SaveAsync(DbManager db) => db.ExecuteAsync(SQLQueries.UPDATE_Компьютеры,
        WriteParameters(), commandType: CommandType.Text);

    internal async Task<int> SaveEnlistedAsync(DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        object parameters;
        try { parameters = WriteParameters(); }
        catch (OverflowException)
        {
            throw new EquipmentComputerWriteRejectedException("Суммарный объём компонентов превышает допустимое значение.");
        }
        var proof = await connection.QuerySingleOrDefaultAsync<ComputerWriteProof>(new CommandDefinition(
            SQLQueries.UPDATE_КомпьютерыEnlisted, parameters, transaction, commandType: CommandType.Text, cancellationToken: ct));
        var rejection = proof?.Status switch
        {
            1 => "Строка компьютера изменилась или была удалена. Обновите данные.",
            2 => "Строка компьютера за выбранную дату уже существует. Обновите данные.",
            3 => "База не подтвердила сохранение снимка компьютера.",
            _ => null,
        };
        if (rejection is not null) throw new EquipmentComputerWriteRejectedException(rejection);
        if (proof is not { Status: 0, ConfirmedEquipmentId: > 0 } || proof.ConfirmedEquipmentId != EquipmentId)
            throw new InvalidOperationException("База не подтвердила точный ключ снимка компьютера.");
        return EquipmentId;
    }

    private sealed class ComputerWriteProof
    {
        /// <summary>Именованный статус сохранения.</summary>
        public int? Status { get; set; }
        /// <summary>Серверный код оборудования подтверждённого снимка.</summary>
        public int? ConfirmedEquipmentId { get; set; }
    }

    private object WriteParameters()
    {
        RamDetail = EquipmentComputerDetail.Serialize(RamComponents, false);
        HdDetail = EquipmentComputerDetail.Serialize(HdComponents, true);
        SsdDetail = EquipmentComputerDetail.Serialize(SsdComponents, false);
        RamMb = checked((int)EquipmentComputerDetail.Sum(RamComponents));
        HdGb = (float)EquipmentComputerDetail.Sum(HdComponents);
        SsdGb = checked((int)EquipmentComputerDetail.Sum(SsdComponents));
        return new
        {
            КодОборудования = EquipmentId,
            С = Date.Date,
            ОжидаетсяСуществующий = ExpectedExisting ? 1 : 0,
            ИсходноеИзменено = ExpectedExisting ? ChangedAt : (DateTime?)null,
            RAM = RamMb,
            RAMDetail = RamDetail,
            HD = HdGb,
            HDDetail = HdDetail,
            SSD = SsdGb,
            SSDDetail = SsdDetail,
            КодСтандартнойКонфигурации1 = Configuration1Id,
            ДатаСтандартнойКонфигурации1 = Configuration1Date,
            КодСтандартнойКонфигурации2 = Configuration2Id,
            ДатаСтандартнойКонфигурации2 = Configuration2Date,
            Лицензия = License ?? string.Empty,
            Примечания = Notes ?? string.Empty,
        };
    }
}

internal sealed class EquipmentComputerWriteRejectedException(string message) : InvalidOperationException(message);
