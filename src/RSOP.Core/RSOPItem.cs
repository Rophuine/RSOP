using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;

namespace RSOP.Core
{
    public abstract class RSOPItem
    {
        protected bool Persisted = false;

        // The default backing store. For ease of coding, all accessed database fields must go in here, even if 
        // the value is not being pulled from here. The ItemDictionary allows defaults, but may not be needed eventually.
        /// <summary>
        /// NEVER access values directly from here. Use this[key] instead. You CAN use this for a list of all data columns by accessing the Keys collection.
        /// </summary>
        private readonly ItemDictionary _fields = new ItemDictionary();

        // The mapped backing store. Columns referenced here use the property value stored rather than the value from
        // the fields Dictionary.
        private readonly SortedDictionary<string, PropertyInfo> _mappedProperties = new SortedDictionary<string, PropertyInfo>();

        // The method for accessing fields, irrespective of their data mapping. Using the fieldname as an index to this
        // accesses the place that field is stored in this object.
        public object this[string field]
        {
            get
            {
                // If there's a mapped property, use it; otherwise, fall back to the unmapped dictionary.
                if (_mappedProperties.ContainsKey(field)) return _mappedProperties[field].GetValue(this, null);
                return _fields[field];
            }
            set
            {
                // If there's a mapped property, store into it; otherwise, fall back to the unmapped dictionary.
                if (_mappedProperties.ContainsKey(field)) _mappedProperties[field].SetValue(this, value, null);
                _fields[field] = value;
            }
        }

        protected RSOPItem()
        {
            foreach (PropertyInfo pi in GetType().GetProperties())
            {
                // Go through all of the properties of the item being instantiated (which is a child of this class)
                foreach (Attribute a in pi.GetCustomAttributes(false))
                {
                    // If the property is marked as having a default value, set that in the unmapped store.
                    var dVal = a as DefaultValueAttribute;
                    if (dVal != null) _fields.AddDefault(pi.Name, dVal.Value);

                    // If the property has a mapping attribute, set the property accessor to use that attribute instead of the unmapped dictionary
                    var map = a as ColumnMapAttribute;
                    if (map != null)
                    {
                        _mappedProperties[map.ColumnName] = pi;
                        _fields[map.ColumnName] = pi.GetValue(this, null);
                    }
                }
            }
        }

        private void RunSingleRowCommand(SqlCommand command)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (SqlTransaction singleRowEnforcer = conn.BeginTransaction())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = singleRowEnforcer;
                        int rowsAffected = command.ExecuteNonQuery();

                        // This should affect one row only
                        if (rowsAffected == 1)
                        {
                            singleRowEnforcer.Commit();
                            Persisted = true;
                        }
                        else
                        {
                            singleRowEnforcer.Rollback();
                            throw new Exception(string.Format("Single-row operation would have instead affected {0} rows. Transaction rolled back.", rowsAffected));
                        }
                    }
                    catch (Exception ex)
                    {
                        singleRowEnforcer.Rollback();
                        throw new Exception("Unable to process single-row operation. Transaction rolled back.", ex);
                    }
                }
            }
        }
        public void Save()
        {
            try
            {
                SqlCommand saveCmd;
                string sql;
                if (Persisted)
                {
                    sql = string.Format("UPDATE {0} SET {1} WHERE {2}", TableName(),
                        GetNonPkParmPairs(), // A list like "fieldname=@fieldname,fieldname2=@fieldname2". Values are not substituted. Use parameters.
                        GetPkCondition());
                    // Get a command with the parameters added for substitution (for each @fieldname in GetNonPKParmPairs, there is "@fieldname:actualvalue")
                    saveCmd = GetParameterizedCommand(sql, null);
                }
                else
                {
                    sql = string.Format("INSERT INTO {0} ({1}) VALUES ({2})", TableName(),
                        GetColumnList(), // just get the column list
                        GetParmList()); // get the column list with each item preceeded by @ for parameter substitution
                    // Get a command with the parameters added for substitution (for each @fieldname in GetNonPKParmPairs, there is "@fieldname:actualvalue")
                    saveCmd = GetParameterizedCommand(sql, null);
                }
                RunSingleRowCommand(saveCmd);
                // If the command didn't succeed, an exception will be thrown. If it succeeded, we will reach the next line and mark the item as persisted.
                Persisted = true;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save row.", ex);
            }
        }
        public void Delete()
        {
            if (Persisted)
            {
                try
                {
                    // Create a delete with the primary key reference as the conditional (no value, sql will read WHERE PKfield=@PKfield)
                    string delString = string.Format("DELETE FROM {0} WHERE {1}", TableName(), GetPkCondition());
                    // Get a command with the parameters set for substitution
                    SqlCommand delCmd = GetParameterizedCommand(delString, null);
                    RunSingleRowCommand(delCmd);
                    //if the above does not succeed it will throw an exception. If it succeeded, mark the item as no longer persisted.
                    Persisted = false;
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to delete row.", ex);
                }
            }
        }

        /// <summary>
        /// Gets a comma-separated list of columns for which this object maintains data
        /// </summary>
        protected string GetColumnList()
        {
            string list = "";
            bool first = true;
            foreach (KeyValuePair<string, object> kvp in _fields)
            {
                if (!first) list += ",";
                first = false;
                list += kvp.Key;
            }
            return list;
        }
        /// <summary>
        /// Get a list of parameters for the object, separated by commas. eg "@column1,@column2,..."
        /// </summary>
        protected string GetParmList()
        {
            string list = "";
            bool first = true;
            foreach (KeyValuePair<string, object> kvp in _fields)
            {
                if (!first) list += ",";
                first = false;
                // Put @key in the list. Do NOT place its value here; use parameters to the SQL command instead.
                list += "@" + kvp.Key;
            }
            return list;
        }

        /// <summary>
        /// Returns a command, parameterizing all data fields with parameter names as "columnname".
        /// </summary>
        /// <param name="sql">SQL command to associate with the command.</param>
        /// <param name="conn">Connection to use</param>
        /// <param name="transaction">Transaction to associate</param>
        protected SqlCommand GetParameterizedCommand(string sql, SqlConnection conn, SqlTransaction transaction)
        {
            SqlCommand command = transaction == null ? new SqlCommand(sql, conn) : new SqlCommand(sql, conn, transaction);
            // For every mapped or unmapped data item...
            foreach (KeyValuePair<string, object> kvp in _fields)
            {
                // Add the parameter with the value from this[key] to get the correct mapping
                command.Parameters.AddWithValue(kvp.Key, this[kvp.Key]);
            }
            return command;

        }
        /// <summary>
        /// Returns a command, parameterizing all data fields with parameter names as "@columnname".
        /// </summary>
        /// <param name="sql">SQL command to associate with the command.</param>
        /// <param name="conn">Connection to use</param>
        protected SqlCommand GetParameterizedCommand(string sql, SqlConnection conn)
        {
            return GetParameterizedCommand(sql, conn, null);
        }

        /// <summary>
        /// Gets a list of columns for which this object maintains data (excluding the primary key), set equal to the parameter name to be used, separated by commas.
        /// For example, "column1=@column1,column2=@column2,..."
        /// </summary>
        protected string GetNonPkParmPairs()
        {
            string pairs = "";
            bool first = true;
            foreach (string key in _fields.Keys)
            {
                if (key.ToUpper() != PrimaryKey().ToUpper())
                {
                    if (!first) pairs += ",";
                    first = false;
                    pairs += string.Format("{0}=@{0}", key);
                }
            }
            return pairs;
        }
        /// <summary>
        /// Get the primary key condition, parameterized ("pkcolumn=@pkcolumn", value not substituted: use parameters)
        /// </summary>
        /// <returns></returns>
        protected string GetPkCondition()
        {
            string cond = "";
            cond += string.Format("{0}=@{0}", PrimaryKey());
            return cond;
        }

        /// <summary>
        /// Return all items of the given type that match an SQL criteria. 
        /// WARNING: This is not parameterized. Do not directly pass user input. SQL injection attacks can occur.
        /// </summary>
        /// <typeparam name="T">Type of object to be returned.</typeparam>
        /// <param name="propName">Name of DATABASE FIELD to be inserted.</param>
        /// <param name="sqlCriteria">SQL to be appended to command after DATABASE FIELD.</param>
        public static List<T> GetBySqlCriteria<T>(string propName, string sqlCriteria) where T : RSOPItem, new()
        {
            string sql = string.Format("SELECT * FROM {0} WHERE {1} {2}", TableName<T>(), propName, sqlCriteria);
            return GetBySql<T>(sql);
        }

        /// <summary>
        /// Return all items of the given type that match a value.
        /// </summary>
        /// <typeparam name="T">Type of object to be returned.</typeparam>
        /// <param name="propName">Name of DATABASE FIELD to be matched</param>
        /// <param name="value">Value to match</param>
        public static List<T> GetByProperty<T>(string propName, object value) where T : RSOPItem, new()
        {
            string sql = string.Format("SELECT * FROM {0} WHERE {1} = @{1}", TableName<T>(), propName);
            var parm = new SqlParameter("@" + propName, value);
            return GetBySql<T>(sql, new List<SqlParameter> { parm });
        }

        /// <summary>
        /// Return a list of all objects of the given type.
        /// </summary>
        private static List<T> GetBySql<T>(string sql) where T : RSOPItem, new() { return GetBySql<T>(sql, null); }

        /// <summary>
        /// Return a list of all objects of the given type matching the constructed SQL and provided parameters.
        /// </summary>
        private static List<T> GetBySql<T>(string sql, List<SqlParameter> parameters) where T : RSOPItem, new()
        {
            var items = new List<T>();
            SqlDataReader reader = GetReader<T>(sql, parameters);
            foreach (IDataRecord record in reader)
            {
                // Instantiate the correct type and set all data members.
                var item = Activator.CreateInstance<T>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = record.GetName(i);
                    item[columnName] = record.GetValue(i);
                }
                // We read it from the database, so it's already persisted. Add to the list and continue.
                item.Persisted = true;
                items.Add(item);
            }
            return items;
        }

        /// <summary>
        /// Return the tablemapping attribute for the given class.
        /// </summary>
        private static object GetTableMappingAttr(Type itemType, string attrName)
        {
            foreach (Attribute attr in itemType.GetCustomAttributes(false))
            {
                // If this attribute is a DatabaseTableMap, return the requested attribute. Do not allow inheritance of these attributes.
                var dbAttr = attr as DatabaseTableMapAttribute;
                if (dbAttr != null)
                {
                    return dbAttr.GetType().GetProperty(attrName).GetValue(dbAttr, null);
                }
            }
            throw new Exception(string.Format("Table mapping string {0} not found in DatabaseTableMapping attribute setup on {1}.", attrName, itemType.Name));
        }

        /// <summary>
        /// Return the tablemapping custom attribute for the given class.
        /// </summary>
        private static object GetTableMappingAttr<T>(string attrName)
        {
            return GetTableMappingAttr(typeof(T), attrName);
        }

        /// <summary>
        /// Return the database configuration custom attribute for the given class.
        /// </summary>
        private static object GetDatabaseConfigurationAttr(Type itemType, string attrName)
        {
            // Go through all attributes on the class. Allow inheritance, to find the base databaseconfig for children of it.
            foreach (Attribute attr in itemType.GetCustomAttributes(true))
            {
                // If this attribute is a DatabaseConfiguration, return the requested attribue
                var dbConfig = attr as DatabaseConfigurationAttribute;
                if (dbConfig != null)
                {
                    return dbConfig.GetType().GetProperty(attrName).GetValue(dbConfig, null);
                }
            }
            throw new Exception(string.Format("Database configuration attribute {0} not found in DatabaseConfiguration attribute on {1}.", attrName, itemType.Name));
        }

        /// <summary>
        /// Return the database configuration custom attribute for the given class.
        /// </summary>
        private static object GetDatabaseConfigurationAttr<T>(string attrName)
        {
            return GetDatabaseConfigurationAttr(typeof(T), attrName);
        }

        /// <summary>
        /// Retrieve the connection string marked up for the ultimate databaseconfigured parent of the object
        /// </summary>
        protected string ConnectionString { get { return GetConnectionString(); } }
        protected string GetConnectionString() { return (string)GetDatabaseConfigurationAttr(GetType(), "ConnectionString"); }
        protected static string GetConnectionString<T>() { return (string)GetDatabaseConfigurationAttr<T>("ConnectionString"); }

        /// <summary>
        /// Return the tablename of whichever object type we're working with
        /// </summary>
        protected string TableName() { return (string)GetTableMappingAttr(GetType(), "TableName"); }
        protected static string TableName<T>() { return (string)GetTableMappingAttr<T>("TableName"); }

        /// <summary>
        /// Return the primary key field of whichever object type we're working with
        /// </summary>
        protected string PrimaryKey() { return (string)GetTableMappingAttr(GetType(), "PrimaryKey"); }
        protected static string PrimaryKey<T>() { return (string)GetTableMappingAttr<T>("PrimaryKey"); }

        /// <summary>
        /// Get all items matching the selected class.
        /// </summary>
        /// <typeparam name="T">Type of object to retrieve</typeparam>
        public static List<T> GetAll<T>() where T : RSOPItem, new()
        {
            string sql = string.Format("SELECT * FROM {0}", TableName<T>());
            return GetBySql<T>(sql);
        }

        /// <summary>
        /// Return a reader for the type and SQL (with parameters) provided.
        /// </summary>
        /// <typeparam name="T">Type of object to be retrieved</typeparam>
        /// <param name="sql">SQL to be used for selected objects</param>
        /// <param name="parameters">SQL Parameters used in the SQL string</param>
        /// <returns>A reader which will automatically close its connection when it is closed</returns>
        protected static SqlDataReader GetReader<T>(string sql, List<SqlParameter> parameters)
        {
            var conn = new SqlConnection(GetConnectionString<T>());
            conn.Open();
            var comm = new SqlCommand(sql, conn);
            // Hook up all provided parameters
            if (parameters != null)
            {
                foreach (SqlParameter parm in parameters) comm.Parameters.Add(parm);
            }
            // Create the reader, set to close its connection when it is closed.
            SqlDataReader results = comm.ExecuteReader(CommandBehavior.CloseConnection);
            return results;
        }
    }
}
