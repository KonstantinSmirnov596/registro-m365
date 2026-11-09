using Azure;
using Azure.Data.Tables;
using Registro.Core.Numeracion;

namespace Registro.Functions.Numeracion;

/// <summary>
/// <see cref="ICounterStore"/> on Azure Table Storage.
/// One entity per register and year: PartitionKey = flow prefix (E/S/I), RowKey = year, property <c>Valor</c>.
/// Table Storage gives exactly the two primitives the algorithm needs:
/// insert fails with 409 if the entity exists, conditional replace fails with 412 if the ETag changed.
/// </summary>
public sealed class TableCounterStore(TableClient table) : ICounterStore
{
    private const string ValueProperty = "Valor";

    private readonly Lazy<Task> _ensureTable = new(() => table.CreateIfNotExistsAsync());

    public async Task<CounterSnapshot?> ReadAsync(string partition, string key, CancellationToken cancellationToken)
    {
        await _ensureTable.Value.ConfigureAwait(false);

        var response = await table
            .GetEntityIfExistsAsync<TableEntity>(partition, key, [ValueProperty], cancellationToken)
            .ConfigureAwait(false);

        if (!response.HasValue || response.Value is null)
        {
            return null;
        }

        var entity = response.Value;
        var value = entity.GetInt64(ValueProperty) ?? 0;
        return new CounterSnapshot(value, entity.ETag.ToString());
    }

    public async Task<bool> TryCreateAsync(string partition, string key, long value, CancellationToken cancellationToken)
    {
        await _ensureTable.Value.ConfigureAwait(false);

        try
        {
            await table
                .AddEntityAsync(new TableEntity(partition, key) { [ValueProperty] = value }, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(string partition, string key, long value, string etag, CancellationToken cancellationToken)
    {
        await _ensureTable.Value.ConfigureAwait(false);

        try
        {
            await table
                .UpdateEntityAsync(
                    new TableEntity(partition, key) { [ValueProperty] = value },
                    new ETag(etag),
                    TableUpdateMode.Replace,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            return false;
        }
    }
}
