using System.Data;
using System.Reflection;

namespace OrderServiceGrpc.Helpers.cs
{
    public static class DataTableConverter
    {
        public static DataTable ToDataTable<T>(List<T> items)
        {
            // 1. Check for null or empty list
            if (items == null || items.Count == 0)
            {
                Console.WriteLine("Warning: Input list is null or empty. Returning empty DataTable.");
                return new DataTable();
            }

            // 2. Get the Type information of the generic parameter T
            Type itemType = typeof(T);

            // 3. Get all public properties of Type T
            // We use BindingFlags to ensure we only get instance (non-static) and public properties.
            PropertyInfo[] properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // 4. Initialize the DataTable
            DataTable dataTable = new DataTable(itemType.Name);

            // 5. Create Columns in the DataTable
            foreach (PropertyInfo prop in properties)
            {
                // Determine the column type. Use the underlying type for Nullable types.
                Type propType = prop.PropertyType;
                Type columnType = Nullable.GetUnderlyingType(propType) ?? propType;

                // Handle the special case where the type might not be supported by DataTable
                // We use object if the type is complex (e.g., another class/List)
                if (columnType.IsClass && columnType != typeof(string))
                {
                    // For simplicity, skip complex types or consider converting them to string/JSON.
                    // In this example, we'll use string representation for complex types.
                    columnType = typeof(string);
                }

                dataTable.Columns.Add(prop.Name, columnType);
            }

            // 6. Populate Rows in the DataTable
            foreach (T item in items)
            {
                DataRow row = dataTable.NewRow();

                foreach (PropertyInfo prop in properties)
                {
                    // Retrieve the value of the property for the current item
                    object value = prop.GetValue(item, null);

                    // Handle DBNull for null values
                    if (value == null)
                    {
                        row[prop.Name] = DBNull.Value;
                    }
                    else
                    {
                        row[prop.Name] = value;
                    }
                }

                dataTable.Rows.Add(row);
            }

            return dataTable;
        }
    }
}
