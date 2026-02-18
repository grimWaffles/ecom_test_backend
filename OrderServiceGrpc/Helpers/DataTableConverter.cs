using OrderServiceGrpc.Models.Entities;
using System.Data;
using System.Reflection;
using System.Text.Json;

namespace OrderServiceGrpc.Helpers.cs
{
    public static class DataTableConverter
    {
        public static DataTable ToDataTable<T>(List<T> items)
        {
            if (items == null || items.Count == 0)
                return new DataTable();

            Type itemType = typeof(T);
            PropertyInfo[] properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                .Where(p => p.CanRead)
                                                .ToArray();

            DataTable dataTable = new DataTable(itemType.Name);

            // Build column schema
            foreach (PropertyInfo prop in properties)
            {
                Type propType = prop.PropertyType;
                Type underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;
                bool isNullable = propType != underlyingType || !propType.IsValueType;

                bool isComplexType = underlyingType.IsClass && underlyingType != typeof(string);
                Type columnType = isComplexType ? typeof(string) : underlyingType;

                DataColumn column = new DataColumn(prop.Name, columnType);
                column.AllowDBNull = isNullable;
                dataTable.Columns.Add(column);
            }

            // Populate rows
            foreach (T item in items)
            {
                if (item == null)
                    continue;

                DataRow row = dataTable.NewRow();
                foreach (PropertyInfo prop in properties)
                {
                    object value;
                    try
                    {
                        value = prop.GetValue(item, null);
                    }
                    catch (Exception ex)
                    {
                        value = $"[Error reading property: {ex.Message}]";
                    }

                    if (value == null)
                    {
                        row[prop.Name] = DBNull.Value;
                    }
                    else
                    {
                        // If the column was typed as string due to complex type, serialize the value
                        bool isComplex = dataTable.Columns[prop.Name].DataType == typeof(string)
                                         && !(value is string);
                        row[prop.Name] = isComplex ? JsonSerializer.Serialize(value) : value;
                    }
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        public static DataTable OrderItemsToDataTable(List<OrderItemModel> items)
        {
            var table = new DataTable("OrderItem");

            // Define Columns
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("OrderId", typeof(int));
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("Quantity", typeof(int));
            table.Columns.Add("GrossAmount", typeof(decimal));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("CreatedBy", typeof(int));
            table.Columns.Add("CreatedDate", typeof(DateTime));
            table.Columns.Add("ModifiedDate", typeof(DateTime));
            table.Columns.Add("ModifiedBy", typeof(int));
            table.Columns.Add("IsDeleted", typeof(bool));
            table.Columns.Add("UnitPrice", typeof(decimal));

            // Populate Rows
            foreach (var item in items)
            {
                table.Rows.Add(
                    item.Id,
                    item.OrderId,
                    item.ProductId,
                    item.Quantity,
                    item.GrossAmount,
                    item.Status ?? string.Empty,
                    item.CreatedBy,
                    item.CreatedDate,
                    item.ModifiedDate,
                    item.ModifiedBy,
                    item.IsDeleted,
                    item.UnitPrice
                );
            }

            return table;
        }
    }
}
