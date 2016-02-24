﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tortuga.Chain.Metadata;

namespace Tortuga.Chain.SQLite.Sqlite
{
    /// <summary>
    /// Handles caching of metadata for various SQLite tables and views.
    /// </summary>
    class SQLiteMetadataCache : DatabaseMetadataCache<string>
    {
        private readonly SQLiteConnectionStringBuilder m_ConnectionBuilder;
        private readonly ConcurrentDictionary<string, TableOrViewMetadata<string>> m_Tables = new ConcurrentDictionary<string, TableOrViewMetadata<string>>();

        /// <summary>
        /// Creates a new instance of <see cref="SQLiteMetadataCache"/>
        /// </summary>
        /// <param name="connectionBuilder">The connection builder.</param>
        public SQLiteMetadataCache(SQLiteConnectionStringBuilder connectionBuilder)
        {
            m_ConnectionBuilder = connectionBuilder;
        }

        /// <summary>
        /// Gets the metadata for a stored procedure.
        /// NOTE:Currently returns null since SQLite doesn't support stored procedures.
        /// </summary>
        /// <param name="procedureName"></param>
        /// <returns></returns>
        public override StoredProcedureMetadata<string> GetStoredProcedure(string procedureName)
        {
            //throw new NotSupportedException();
            return null;
        }

        /// <summary>
        /// Gets the metadata for a table function.
        /// NOTE:Currently returns null since SQLite doesn't support stored procedures. 
        /// </summary>
        /// <param name="tableFunctionName"></param>
        /// <returns></returns>
        public override TableFunctionMetadata<string> GetTableFunction(string tableFunctionName)
        {
            //throw new NotSupportedException();
            return null; 
        }

        /// <summary>
        /// Gets the metadata for a table or view.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public override TableOrViewMetadata<string> GetTableOrView(string tableName)
        {
            return m_Tables.GetOrAdd(tableName, GetTableOrViewInternal);
        }

        private TableOrViewMetadata<string> GetTableOrViewInternal(string tableName)
        {
            const string tableSql =
                @"SELECT 
                type AS ObjectType,
                tbl_name AS ObjectName
                FROM sqlite_master
                WHERE tbl_name = @Name";

            string actualName;
            bool isTable;

            using (var con = new SQLiteConnection(m_ConnectionBuilder.ConnectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand(tableSql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", tableName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;
                        actualName = reader.GetString(reader.GetOrdinal("ObjectName"));
                        var objectType = reader.GetString(reader.GetOrdinal("ObjectType"));
                        isTable = objectType.Equals("table");
                    }
                }
            }

            var columns = GetColumns(tableName);
            return new TableOrViewMetadata<string>(tableName, isTable, columns);
        }

        /// <summary>
        /// Preloads metadata for all database tables.
        /// </summary>
        /// <remarks>This is normally used only for testing. By default, metadata is loaded as needed.</remarks>
        public void PreloadTables()
        {
            const string tableSql =
                @"SELECT
                tbl_name as TableName
                FROM sqlite_master
                WHERE type = 'table'";

            using (var con = new SQLiteConnection(m_ConnectionBuilder.ConnectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand(tableSql, con))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            var tableName = reader.GetString(reader.GetOrdinal("TableName"));
                            GetTableOrView(tableName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Preloads metadata for all database views.
        /// </summary>
        /// <remarks>This is normally used only for testing. By default, metadata is loaded as needed.</remarks>
        public void PreloadViews()
        {
            const string viewSql =
                @"SELECT
                tbl_name as ViewName
                FROM sqlite_master
                WHERE type = 'view'";

            using (var con = new SQLiteConnection(m_ConnectionBuilder.ConnectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand(viewSql, con))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var viewName = reader.GetString(reader.GetOrdinal("ViewName"));
                            GetTableOrView(viewName);
                        }
                    }
                }
            }
        }

        private List<ColumnMetadata> GetColumns(string tableName)
        {
            const string columnSql = "PRAGMA table_info(@TableName)";

            var columns = new List<ColumnMetadata>();
            using (var con = new SQLiteConnection(m_ConnectionBuilder.ConnectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand(columnSql, con))
                {
                    cmd.Parameters.AddWithValue("@TableName", tableName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            var name = reader.GetString(reader.GetOrdinal("name"));
                            var typeName = reader.GetString(reader.GetOrdinal("type"));
                            var isPrimaryKey = reader.GetInt32(reader.GetOrdinal("pk")) != 0 ? true : false;

                            columns.Add(new ColumnMetadata(name, false, isPrimaryKey, false, typeName));
                        }
                    }
                }
            }

            return columns;
        }
    }
}
